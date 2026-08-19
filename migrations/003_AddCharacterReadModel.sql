CREATE TABLE IF NOT EXISTS character_read_model (
    character_id UUID PRIMARY KEY,
    name TEXT NOT NULL,
    level INT NOT NULL DEFAULT 1,
    class_name TEXT,
    race TEXT,
    current_hp INT NOT NULL,
    max_hp INT NOT NULL,
    is_dead BOOLEAN NOT NULL DEFAULT FALSE,
    gold INT NOT NULL DEFAULT 0,
    last_updated TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_character_read_model_name ON character_read_model(name);
CREATE INDEX IF NOT EXISTS idx_character_read_model_level ON character_read_model(level);