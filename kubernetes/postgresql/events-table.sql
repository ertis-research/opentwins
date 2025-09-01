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