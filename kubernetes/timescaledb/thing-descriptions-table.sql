CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

CREATE TABLE thing_descriptions (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    thingId TEXT UNIQUE,
    td JSONB,
	created_at TIMESTAMPTZ DEFAULT now(),
    last_update TIMESTAMPTZ DEFAULT now()
);

-- Esto es para que last_update se actualice solo
CREATE OR REPLACE FUNCTION update_last_update_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.last_update = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER set_last_update
BEFORE UPDATE ON thing_descriptions
FOR EACH ROW
EXECUTE FUNCTION update_last_update_column();