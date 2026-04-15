-- =====================================================
-- KEYSPACE
-- =====================================================
CREATE KEYSPACE IF NOT EXISTS npp
WITH replication = {
    'class': 'SimpleStrategy',
    'replication_factor': 1
};

USE npp;

-- =====================================================
-- PLAYERS (osnovni podaci o igraču)
-- =====================================================
CREATE TABLE IF NOT EXISTS players (
    player_id uuid PRIMARY KEY,
    username text,
    email text,
    avatar_url text,
    created_at timestamp
);

-- =====================================================
-- PLAYERS BY EMAIL (login / autentifikacija)
-- =====================================================
CREATE TABLE IF NOT EXISTS players_by_email (
    email text PRIMARY KEY,
    password_hash text,
    player_id uuid
);

-- =====================================================
-- PLAYERS BY USERNAME (lookup by username)
-- =====================================================
CREATE TABLE IF NOT EXISTS players_by_username (
    username text PRIMARY KEY,
    player_id uuid
);

-- =====================================================
-- MATCH HISTORY (mečevi po igraču i godini)
-- =====================================================
CREATE TABLE IF NOT EXISTS player_matches (
    player_id uuid,
    year text,                    -- npr. "2026"
    match_time timestamp,
    match_id uuid,
    opponent_id uuid,
    opponent_username text,       -- denormalizacija radi brzine
    score text,                   -- npr. "11:7"
    result text,                  -- WIN / LOSS
    PRIMARY KEY ((player_id, year), match_time, match_id)
) WITH CLUSTERING ORDER BY (match_time DESC);

-- =====================================================
-- PLAYER STATS (ukupna statistika)
-- =====================================================
CREATE TABLE IF NOT EXISTS player_stats (
    player_id uuid PRIMARY KEY,
    total_points counter,
    games_won counter,
    games_lost counter
);

-- =====================================================
-- MONTHLY LEADERBOARD (statistika po mesecu)
-- =====================================================
CREATE TABLE IF NOT EXISTS monthly_leaderboard (
    year_month text,              -- npr. "2026-01"
    player_id uuid,
    total_points counter,
    PRIMARY KEY (year_month, player_id)
);

-- =====================================================
-- YEARLY LEADERBOARD (statistika po godini)
-- =====================================================
CREATE TABLE IF NOT EXISTS yearly_leaderboard (
    year text,                    -- npr. "2026"
    player_id uuid,
    total_points counter,
    PRIMARY KEY (year, player_id)
);

-- =====================================================
-- GLOBAL LEADERBOARD (po periodima)
-- =====================================================
CREATE TABLE IF NOT EXISTS global_leaderboard (
    period_type text,     -- npr. 'MONTHLY', 'YEARLY', 'ALL_TIME'
    period_id text,       -- npr. '2024-01'
    rank_score int,       -- vrednost po kojoj rangiramo (npr. broj pobeda)
    player_id uuid,
    username text,
    PRIMARY KEY ((period_type, period_id), rank_score, player_id)
) WITH CLUSTERING ORDER BY (rank_score DESC);

-- =====================================================
-- GLOBAL LEADERBOARD BY PLAYER (lookup for updates)
-- =====================================================
CREATE TABLE IF NOT EXISTS global_leaderboard_by_player (
    period_type text,
    period_id text,
    player_id uuid,
    username text,
    rank_score int,
    PRIMARY KEY ((period_type, period_id), player_id)
);

-- =====================================================
-- WINS LEADERBOARD (po broju pobeda)
-- =====================================================
CREATE TABLE IF NOT EXISTS leaderboard_by_wins (
    category text,        -- npr. 'most_wins'
    games_won int,
    player_id uuid,
    username text,
    PRIMARY KEY (category, games_won, player_id)
) WITH CLUSTERING ORDER BY (games_won DESC);

-- =====================================================
-- WINS LEADERBOARD BY PLAYER (lookup for updates)
-- =====================================================
CREATE TABLE IF NOT EXISTS leaderboard_by_wins_by_player (
    category text,
    player_id uuid,
    username text,
    games_won int,
    PRIMARY KEY ((category), player_id)
);

-- =====================================================
-- PLAYER CURRENT STREAK (trenutni streak igrača)
-- =====================================================
CREATE TABLE IF NOT EXISTS player_current_streak (
    player_id uuid PRIMARY KEY,
    username text,
    current_streak int,
    longest_streak int,
    last_result text -- 'WIN' ili 'LOSS'
);

-- =====================================================
-- LONGEST STREAK LEADERBOARD (po najdužem streaku)
-- =====================================================
CREATE TABLE IF NOT EXISTS leaderboard_by_longest_streak (
    category text,           -- npr. 'global_all_time'
    longest_streak int,
    player_id uuid,
    username text,
    PRIMARY KEY (category, longest_streak, player_id)
) WITH CLUSTERING ORDER BY (longest_streak DESC);

-- =====================================================
-- LONGEST STREAK BY PLAYER (lookup for updates)
-- =====================================================
CREATE TABLE IF NOT EXISTS leaderboard_by_longest_streak_by_player (
    category text,
    player_id uuid,
    username text,
    longest_streak int,
    PRIMARY KEY ((category), player_id)
);

CREATE TABLE IF NOT EXISTS recent_matches (
    period text,                 -- "2024-01" 
    match_time timestamp,
    match_id uuid,
    player1_username text,
    player2_username text,
    score text,
    result text,                 -- Ko je pobedio
    PRIMARY KEY ((period), match_time, match_id)
) WITH CLUSTERING ORDER BY (match_time DESC);

-- =====================================================
-- REDIS STRUCTURES (DOKUMENTACIJA)
-- =====================================================
-- Redis nema CQL tabele, ali ovo je dokumentacija
-- ključeva koji se koriste u sistemu
--
-- 1) GLOBAL LEADERBOARD (Sorted Set)
--    Key: leaderboard:global
--    ZADD leaderboard:global 0 player_id        (registracija)
--    ZINCRBY leaderboard:global 50 player_id    (tokom meča)
--    ZREVRANGE leaderboard:global 0 9 WITHSCORES
--
-- 2) MONTHLY LEADERBOARD (Sorted Set)
--    Key: leaderboard:{YYYY-MM}
--    ZINCRBY leaderboard:2026-01 50 player_id
--    ZREVRANGE leaderboard:2026-01 0 9 WITHSCORES
--
-- 3) ACTIVE GAME STATE (Hash)
--    Key: game:{gameId}
--    Fields:
--      ball_x
--      ball_y
--      p1_y
--      p2_y
--      p1_score
--      p2_score
--      state        -- WAITING | RUNNING | FINISHED
--
-- Primer:
-- HSET game:123 ball_x 120 ball_y 300 p1_y 200 p2_y 210 p1_score 3 p2_score 5 state RUNNING
