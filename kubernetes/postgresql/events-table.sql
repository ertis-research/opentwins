CREATE TABLE events (
	id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name TEXT UNIQUE
);

CREATE TABLE topics (
	id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name TEXT UNIQUE
);

CREATE TABLE EventsThings (
    thingId BIGINT NOT NULL,
    eventId BIGINT NOT NULL,
    PRIMARY KEY (eventId, thingId),
    FOREIGN KEY (eventId) REFERENCES events(id),
    FOREIGN KEY (thingId) REFERENCES thing_descriptions(id)
);

CREATE TABLE TopicsEvents (
    topicId BIGINT NOT NULL,
    eventId BIGINT NOT NULL,
    PRIMARY KEY (topicId, eventId),
    FOREIGN KEY (topicId) REFERENCES topics(id),
    FOREIGN KEY (eventId) REFERENCES events(id)
);

ALTER TABLE eventsthings
DROP CONSTRAINT eventsthings_thingid_fkey,
ADD CONSTRAINT eventsthings_thingid_fkey FOREIGN KEY (thingId) REFERENCES thing_descriptions(id) ON DELETE CASCADE;

ALTER TABLE topicsevents
DROP CONSTRAINT topicsevents_eventid_fkey,
ADD CONSTRAINT topicsevents_eventid_fkey FOREIGN KEY (eventId) REFERENCES events(id) ON DELETE CASCADE;
