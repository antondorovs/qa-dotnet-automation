CREATE TABLE tickets (
    id uuid PRIMARY KEY,
    title text NOT NULL CHECK (length(trim(title)) BETWEEN 1 AND 120),
    description text NOT NULL DEFAULT '',
    priority text NOT NULL CHECK (priority IN ('Low', 'Normal', 'High')),
    status text NOT NULL DEFAULT 'Open' CHECK (status IN ('Open', 'InProgress', 'Resolved'))
);
