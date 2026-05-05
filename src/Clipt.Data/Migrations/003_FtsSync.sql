
-- Drop the incomplete FTS table from migration 002 (if it exists)
DROP TABLE IF EXISTS clipboard_items_fts;

-- Create a proper FTS5 table with item_id as a stored (unindexed) link column
-- plus title, preview_text, content_text, and content_type as full-text indexed columns.
CREATE VIRTUAL TABLE clipboard_items_fts USING fts5(
    item_id UNINDEXED,
    title,
    preview_text,
    content_text,
    content_type
);

-- Backfill FTS from existing clipboard_items rows.
INSERT INTO clipboard_items_fts (item_id, title, preview_text, content_text, content_type)
SELECT id, title, preview_text, content_text, content_type
FROM clipboard_items;

-- Triggers to keep FTS in sync with the main clipboard_items table.

-- INSERT trigger
CREATE TRIGGER IF NOT EXISTS trg_clipboard_items_fts_ai
AFTER INSERT ON clipboard_items
BEGIN
    INSERT INTO clipboard_items_fts (item_id, title, preview_text, content_text, content_type)
    VALUES (new.id, new.title, new.preview_text, new.content_text, new.content_type);
END;

-- DELETE trigger
CREATE TRIGGER IF NOT EXISTS trg_clipboard_items_fts_ad
AFTER DELETE ON clipboard_items
BEGIN
    DELETE FROM clipboard_items_fts WHERE item_id = old.id;
END;

-- UPDATE trigger: delete then re-insert, so any column change is reflected.
CREATE TRIGGER IF NOT EXISTS trg_clipboard_items_fts_au
AFTER UPDATE ON clipboard_items
BEGIN
    DELETE FROM clipboard_items_fts WHERE item_id = old.id;
    INSERT INTO clipboard_items_fts (item_id, title, preview_text, content_text, content_type)
    VALUES (new.id, new.title, new.preview_text, new.content_text, new.content_type);
END;
