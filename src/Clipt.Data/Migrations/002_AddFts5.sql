CREATE VIRTUAL TABLE IF NOT EXISTS clipboard_items_fts USING fts5(
    preview_text,
    content_text,
    content_type
);
