CREATE TABLE tickets (
    id uuid PRIMARY KEY,
    title text NOT NULL CHECK (length(trim(title)) BETWEEN 1 AND 120),
    description text NOT NULL DEFAULT '',
    priority text NOT NULL CHECK (priority IN ('Low', 'Normal', 'High')),
    status text NOT NULL DEFAULT 'Open' CHECK (status IN ('Open', 'InProgress', 'Resolved'))
);

CREATE TABLE comments (
    id uuid PRIMARY KEY,
    ticket_id uuid NOT NULL REFERENCES tickets(id) ON DELETE CASCADE,
    body text NOT NULL CHECK (length(trim(body)) BETWEEN 1 AND 2000),
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX comments_ticket_id_idx ON comments(ticket_id);
