using Dapper;
using Microsoft.Data.Sqlite;
using Mhop.Configuration;

namespace Mhop.Data;

/// <summary>
/// SQLite 连接工厂 + 建表/补列迁移。
/// 表结构、索引名与 Python SQLAlchemy 版本保持一致，可直接打开同一个 mhop.db 文件。
/// </summary>
public sealed class Database
{
    private readonly string _connectionString;

    public Database(AppSettings settings)
    {
        if (!settings.IsSqlite)
            throw new NotSupportedException("C# 版当前仅支持 SQLite（与线上部署一致）。");
        var path = settings.SqlitePath;
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
    }

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON";
            pragma.ExecuteNonQuery();
        }
        return conn;
    }

    public void CreateTables()
    {
        using var conn = Open();
        conn.Execute("""
CREATE TABLE IF NOT EXISTS users (
    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    username VARCHAR(64) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    email VARCHAR(255),
    phone VARCHAR(20),
    role VARCHAR(16) NOT NULL DEFAULT 'user',
    status VARCHAR(16) NOT NULL DEFAULT 'active',
    avatar VARCHAR(255) NOT NULL DEFAULT '',
    badge VARCHAR(64) NOT NULL DEFAULT '',
    created_at DATETIME
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_users_username ON users (username);
CREATE UNIQUE INDEX IF NOT EXISTS ix_users_email ON users (email);
CREATE UNIQUE INDEX IF NOT EXISTS ix_users_phone ON users (phone);

CREATE TABLE IF NOT EXISTS posts (
    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER,
    is_anonymous BOOLEAN NOT NULL DEFAULT 1,
    content TEXT NOT NULL,
    board VARCHAR(16) NOT NULL DEFAULT 'mood',
    images TEXT DEFAULT '',
    status INTEGER NOT NULL DEFAULT 0,
    crisis BOOLEAN NOT NULL DEFAULT 0,
    view_count INTEGER NOT NULL DEFAULT 0,
    review_note VARCHAR(255) DEFAULT '',
    created_at DATETIME,
    FOREIGN KEY(user_id) REFERENCES users (id)
);
CREATE INDEX IF NOT EXISTS ix_posts_user_id ON posts (user_id);
CREATE INDEX IF NOT EXISTS ix_posts_board ON posts (board);
CREATE INDEX IF NOT EXISTS ix_posts_status ON posts (status);
CREATE INDEX IF NOT EXISTS ix_posts_created_at ON posts (created_at);

CREATE TABLE IF NOT EXISTS replies (
    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    post_id INTEGER NOT NULL,
    user_id INTEGER,
    is_anonymous BOOLEAN NOT NULL DEFAULT 1,
    content TEXT NOT NULL,
    images TEXT DEFAULT '',
    status INTEGER NOT NULL DEFAULT 0,
    is_ai BOOLEAN NOT NULL DEFAULT 0,
    crisis BOOLEAN NOT NULL DEFAULT 0,
    review_note VARCHAR(255) DEFAULT '',
    recalled BOOLEAN NOT NULL DEFAULT 0,
    recall_reason VARCHAR(255) DEFAULT '',
    created_at DATETIME,
    FOREIGN KEY(post_id) REFERENCES posts (id),
    FOREIGN KEY(user_id) REFERENCES users (id)
);
CREATE INDEX IF NOT EXISTS ix_replies_post_id ON replies (post_id);
CREATE INDEX IF NOT EXISTS ix_replies_status ON replies (status);
CREATE INDEX IF NOT EXISTS ix_replies_recalled ON replies (recalled);

CREATE TABLE IF NOT EXISTS likes (
    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    target_type VARCHAR(8) NOT NULL,
    target_id INTEGER NOT NULL,
    created_at DATETIME,
    FOREIGN KEY(user_id) REFERENCES users (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_like_user_target ON likes (user_id, target_type, target_id);
CREATE INDEX IF NOT EXISTS ix_likes_user_id ON likes (user_id);
CREATE INDEX IF NOT EXISTS ix_like_target ON likes (target_type, target_id);

CREATE TABLE IF NOT EXISTS assessments (
    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER,
    assessment_type VARCHAR(32) NOT NULL,
    input_data TEXT DEFAULT '',
    ai_result TEXT DEFAULT '',
    score INTEGER,
    level VARCHAR(32) DEFAULT '',
    created_at DATETIME,
    FOREIGN KEY(user_id) REFERENCES users (id)
);
CREATE INDEX IF NOT EXISTS ix_assessments_user_id ON assessments (user_id);

CREATE TABLE IF NOT EXISTS ai_logs (
    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER,
    session_id VARCHAR(64) DEFAULT '',
    module VARCHAR(32) NOT NULL,
    reply_id INTEGER,
    prompt TEXT DEFAULT '',
    response TEXT DEFAULT '',
    engine VARCHAR(32) DEFAULT 'local',
    created_at DATETIME,
    FOREIGN KEY(user_id) REFERENCES users (id),
    FOREIGN KEY(reply_id) REFERENCES replies (id)
);
CREATE INDEX IF NOT EXISTS ix_ai_logs_user_id ON ai_logs (user_id);
CREATE INDEX IF NOT EXISTS ix_ai_logs_session_id ON ai_logs (session_id);
CREATE INDEX IF NOT EXISTS ix_ai_logs_reply_id ON ai_logs (reply_id);
""");
    }

    /// <summary>为老库补充新增列（幂等），与 Python migrate_schema 完全对应。</summary>
    public void MigrateSchema()
    {
        using var conn = Open();
        var tables = conn.Query<string>(
            "SELECT name FROM sqlite_master WHERE type='table'").ToHashSet();
        var alters = new List<string>();

        if (tables.Contains("replies"))
        {
            var cols = Columns(conn, "replies");
            if (!cols.Contains("recalled"))
                alters.Add("ALTER TABLE replies ADD COLUMN recalled BOOLEAN NOT NULL DEFAULT 0");
            if (!cols.Contains("recall_reason"))
                alters.Add("ALTER TABLE replies ADD COLUMN recall_reason VARCHAR(255) DEFAULT ''");
            if (!cols.Contains("images"))
                alters.Add("ALTER TABLE replies ADD COLUMN images TEXT DEFAULT ''");
        }
        if (tables.Contains("posts"))
        {
            var cols = Columns(conn, "posts");
            if (!cols.Contains("board"))
                alters.Add("ALTER TABLE posts ADD COLUMN board VARCHAR(16) NOT NULL DEFAULT 'mood'");
            if (!cols.Contains("view_count"))
                alters.Add("ALTER TABLE posts ADD COLUMN view_count INTEGER NOT NULL DEFAULT 0");
            if (!cols.Contains("images"))
                alters.Add("ALTER TABLE posts ADD COLUMN images TEXT DEFAULT ''");
        }
        if (tables.Contains("users"))
        {
            var cols = Columns(conn, "users");
            if (!cols.Contains("email"))
                alters.Add("ALTER TABLE users ADD COLUMN email VARCHAR(255)");
            if (!cols.Contains("avatar"))
                alters.Add("ALTER TABLE users ADD COLUMN avatar VARCHAR(255) NOT NULL DEFAULT ''");
            if (!cols.Contains("badge"))
                alters.Add("ALTER TABLE users ADD COLUMN badge VARCHAR(64) NOT NULL DEFAULT ''");
            if (!cols.Contains("phone"))
                alters.Add("ALTER TABLE users ADD COLUMN phone VARCHAR(20)");
        }
        if (tables.Contains("ai_logs") && !Columns(conn, "ai_logs").Contains("reply_id"))
            alters.Add("ALTER TABLE ai_logs ADD COLUMN reply_id INTEGER");

        foreach (var sql in alters)
            conn.Execute(sql);

        // 老库补 email 唯一索引
        if (tables.Contains("users"))
        {
            var idx = conn.Query<string>(
                "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='users'").ToHashSet();
            if (!idx.Contains("ix_users_email"))
                conn.Execute("CREATE UNIQUE INDEX ix_users_email ON users (email)");
        }
    }

    private static HashSet<string> Columns(SqliteConnection conn, string table) =>
        conn.Query($"PRAGMA table_info({table})")
            .Select(r => (string)((IDictionary<string, object>)r)["name"])
            .ToHashSet();
}
