-- Добавляем таблицу для read-модели кампаний (если нужно)
CREATE TABLE IF NOT EXISTS campaign_read_model (
    campaign_id UUID PRIMARY KEY,
    name TEXT NOT NULL,
    game_master_id UUID NOT NULL,
    current_day INT NOT NULL DEFAULT 1,
    current_hour INT NOT NULL DEFAULT 8,
    current_minute INT NOT NULL DEFAULT 0,
    weather TEXT NOT NULL DEFAULT 'Clear',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Индекс для быстрого поиска по мастеру
CREATE INDEX IF NOT EXISTS idx_campaign_read_model_gm ON campaign_read_model(game_master_id);