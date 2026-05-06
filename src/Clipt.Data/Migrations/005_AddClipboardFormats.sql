CREATE TABLE IF NOT EXISTS clipboard_formats (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    item_id TEXT NOT NULL,
    format_name TEXT NOT NULL,
    payload BLOB NULL,
    payload_text TEXT NULL,
    byte_size INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (item_id) REFERENCES clipboard_items(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_clipboard_formats_item_id
ON clipboard_formats(item_id);

CREATE INDEX IF NOT EXISTS ix_clipboard_formats_item_format
ON clipboard_formats(item_id, format_name);
