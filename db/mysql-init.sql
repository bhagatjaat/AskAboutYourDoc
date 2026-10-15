-- AskMyDocs ka MySQL setup. Ek baar chalana hai.
--
--   /usr/local/mysql/bin/mysql -u root -p < db/mysql-init.sql
--
-- Root password maangega - wahi jo MySQL install karte waqt rakha tha.
-- Neeche 'askmydocs_pass' ko apne password se badal lo, aur wahi password
-- connection string (user-secrets) me bhi daalna.

CREATE DATABASE IF NOT EXISTS askmydocs;

-- App root se connect nahi karegi. Uska apna user hai jise sirf
-- isi database par haq hai - galti se bhi doosre database nahi chhoo sakti.
CREATE USER IF NOT EXISTS 'askmydocs'@'localhost' IDENTIFIED BY 'askmydocs_pass';
GRANT SELECT, INSERT, UPDATE, DELETE ON askmydocs.* TO 'askmydocs'@'localhost';

USE askmydocs;

-- C# ka DocumentSummary record = is table ki ek row.
CREATE TABLE IF NOT EXISTS documents (
    id           CHAR(32)     NOT NULL,   -- Guid "N" format: 32 hex characters, bina dash
    file_name    VARCHAR(255) NOT NULL,
    chunk_count  INT          NOT NULL,
    uploaded_at  DATETIME(6)  NOT NULL,   -- hamesha UTC. (6) = microseconds tak
    PRIMARY KEY (id),
    INDEX ix_documents_uploaded_at (uploaded_at)   -- list isi par sort hoti hai
);
