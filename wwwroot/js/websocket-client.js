// wwwroot/js/websocket-client.js
// Универсальный WebSocket-клиент с поддержкой аутентификации, отправки команд и событий

class WebSocketClient {
    /**
     * @param {string} url - WS URL (с токеном в query-строке)
     * @param {Object} options
     * @param {Function} options.onOpen - вызывается при открытии
     * @param {Function} options.onClose - вызывается при закрытии
     * @param {Function} options.onError - вызывается при ошибке
     * @param {Function} options.onEvent - вызывается при получении события (eventType, eventData)
     * @param {Function} options.onCommandResponse - вызывается при ответе на команду (correlationId, success, resultJson, errorMessage)
     * @param {Function} options.onErrorResponse - вызывается при ошибке (errorCode, message, detail)
     */
    constructor(url, options = {}) {
        this.url = url;
        this.options = options;
        this.ws = null;
        this.connected = false;
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = 5;
        this.reconnectDelay = 2000;
        this.pingInterval = null;
        this.pongTimeout = null;
        this.isClosing = false;
    }

    connect() {
        if (this.ws && (this.ws.readyState === WebSocket.OPEN || this.ws.readyState === WebSocket.CONNECTING)) {
            return;
        }
        this.isClosing = false;
        try {
            this.ws = new WebSocket(this.url);
            this.ws.onopen = (event) => this._onOpen(event);
            this.ws.onmessage = (event) => this._onMessage(event);
            this.ws.onclose = (event) => this._onClose(event);
            this.ws.onerror = (event) => this._onError(event);
        } catch (err) {
            this._onError(err);
        }
    }

    disconnect() {
        this.isClosing = true;
        if (this.ws) {
            if (this.ws.readyState === WebSocket.OPEN) {
                this.ws.close(1000, 'Normal closure');
            } else {
                this.ws = null;
            }
        }
        this.connected = false;
        this._clearKeepAlive();
    }

    send(message) {
        if (!this.connected || !this.ws || this.ws.readyState !== WebSocket.OPEN) {
            console.warn('WebSocket not connected, message not sent');
            return false;
        }
        try {
            this.ws.send(JSON.stringify(message));
            return true;
        } catch (e) {
            console.error('Send error:', e);
            return false;
        }
    }

    sendCommand(commandType, commandJson, correlationId = null) {
        return this.send({
            type: 'command',
            correlationId,
            payload: {
                commandType,
                commandJson
            }
        });
    }

    sendQuery(queryType, queryJson, correlationId = null) {
        return this.send({
            type: 'query',
            correlationId,
            payload: {
                queryType,
                queryJson
            }
        });
    }

    sendUndo(correlationId = null) {
        return this.send({ type: 'undo_request', correlationId });
    }

    sendRedo(correlationId = null) {
        return this.send({ type: 'redo_request', correlationId });
    }

    // ---------- приватные методы ----------

    _onOpen(event) {
        this.connected = true;
        this.reconnectAttempts = 0;
        this._startKeepAlive();
        if (this.options.onOpen) this.options.onOpen(event);
    }

    _onMessage(event) {
        let data;
        try {
            data = JSON.parse(event.data);
        } catch (e) {
            console.warn('Invalid JSON:', event.data);
            return;
        }

        // Проверяем, что это сообщение с типом
        const type = data.type;
        const correlationId = data.correlationId || null;

        switch (type) {
            case 'event':
                // eventType и eventJson в payload
                const eventType = data.payload?.eventType || null;
                const eventJson = data.payload?.eventJson || null;
                if (eventType && eventJson) {
                    try {
                        const eventData = JSON.parse(eventJson);
                        if (this.options.onEvent) {
                            this.options.onEvent(eventType, eventData, correlationId);
                        }
                    } catch (e) {
                        console.warn('Failed to parse event JSON:', eventJson);
                    }
                }
                break;

            case 'command_response':
                const success = data.payload?.success ?? false;
                const resultJson = data.payload?.resultJson || null;
                const errorMessage = data.payload?.errorMessage || null;
                if (this.options.onCommandResponse) {
                    this.options.onCommandResponse(correlationId, success, resultJson, errorMessage);
                }
                break;

            case 'query_response':
                const qSuccess = data.payload?.success ?? false;
                const qResultJson = data.payload?.resultJson || null;
                const qError = data.payload?.errorMessage || null;
                if (this.options.onQueryResponse) {
                    this.options.onQueryResponse(correlationId, qSuccess, qResultJson, qError);
                }
                break;

            case 'error':
                const errorCode = data.errorCode || 'UNKNOWN';
                const message = data.message || 'Unknown error';
                const detail = data.detail || null;
                if (this.options.onErrorResponse) {
                    this.options.onErrorResponse(errorCode, message, detail, correlationId);
                }
                break;

            case 'ping':
                // Отвечаем pong
                this.send({ type: 'pong', correlationId });
                break;

            case 'pong':
                // Игнорируем или сбрасываем таймаут
                this._resetPongTimeout();
                break;

            case 'undo_response':
            case 'redo_response':
                const undoSuccess = data.payload?.success ?? false;
                const undoError = data.payload?.errorMessage || null;
                if (this.options.onUndoResponse) {
                    this.options.onUndoResponse(type === 'undo_response', correlationId, undoSuccess, undoError);
                }
                break;
            case 'chat':
                const chatPayload = data.payload;
                if (this.options.onChat) this.options.onChat(chatPayload);
                break;

            default:
                console.warn('Unknown message type:', type, data);
        }
    }

    _onClose(event) {
        this.connected = false;
        this._clearKeepAlive();
        if (this.options.onClose) this.options.onClose(event);
        if (!this.isClosing && this.reconnectAttempts < this.maxReconnectAttempts) {
            this.reconnectAttempts++;
            const delay = this.reconnectDelay * Math.pow(1.5, this.reconnectAttempts - 1);
            console.log(`Reconnecting in ${delay}ms (attempt ${this.reconnectAttempts})`);
            setTimeout(() => this.connect(), delay);
        }
    }

    _onError(error) {
        if (this.options.onError) this.options.onError(error);
        // Если ошибка произошла при открытии, возможно, нужно закрыть и переподключиться
        if (this.ws && this.ws.readyState === WebSocket.CLOSED) {
            this._onClose(new CloseEvent('error'));
        }
    }

    _startKeepAlive() {
        this._clearKeepAlive();
        this.pingInterval = setInterval(() => {
            if (this.connected) {
                this.send({ type: 'ping' });
                // Устанавливаем таймаут на pong
                this.pongTimeout = setTimeout(() => {
                    console.warn('Pong timeout, reconnecting...');
                    this.disconnect();
                    setTimeout(() => this.connect(), 500);
                }, 5000);
            }
        }, 30000);
    }

    _clearKeepAlive() {
        if (this.pingInterval) {
            clearInterval(this.pingInterval);
            this.pingInterval = null;
        }
        if (this.pongTimeout) {
            clearTimeout(this.pongTimeout);
            this.pongTimeout = null;
        }
    }

    _resetPongTimeout() {
        if (this.pongTimeout) {
            clearTimeout(this.pongTimeout);
            this.pongTimeout = null;
        }
    }
}