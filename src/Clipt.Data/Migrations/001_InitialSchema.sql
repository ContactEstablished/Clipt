CREATE TABLE IF NOT EXISTS clipboard_items (
    id TEXT PRIMARY KEY,
    content_hash TEXT NOT NULL UNIQUE,
    primary_format TEXT NOT NULL,
    content_type TEXT NOT NULL,
    title TEXT NOT NULL,
    preview_text TEXT NOT NULL,
    content_text TEXT NOT NULL,
    source_app_name TEXT NULL,
    source_app_path TEXT NULL,
    byte_size INTEGER NOT NULL,
    is_pinned INTEGER NOT NULL DEFAULT 0,
    pin_order INTEGER NULL,
    is_favorite INTEGER NOT NULL DEFAULT 0,
    created_at INTEGER NOT NULL,
    last_used_at INTEGER NOT NULL,
    use_count INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS ix_clipboard_items_created_at
ON clipboard_items(created_at DESC);

CREATE INDEX IF NOT EXISTS ix_clipboard_items_is_pinned
ON clipboard_items(is_pinned, pin_order);
