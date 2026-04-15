USE npp;

SELECT * FROM global_leaderboard WHERE period_type = 'MONTHLY' AND period_id = '2026-01';
SELECT * FROM leaderboard_by_wins WHERE category = 'most_wins' LIMIT 5;
SELECT * FROM leaderboard_by_longest_streak WHERE category = 'global_all_time' LIMIT 5;
