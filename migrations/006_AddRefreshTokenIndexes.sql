-- Индекс для быстрого удаления истёкших токенов
CREATE INDEX IF NOT EXISTS idx_refresh_tokens_expires_at ON refresh_tokens(expires_at);

-- Частичный индекс для неотозванных токенов
CREATE INDEX IF NOT EXISTS idx_refresh_tokens_active ON refresh_tokens(expires_at) WHERE is_revoked = FALSE;