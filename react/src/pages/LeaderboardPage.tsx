import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { LeaderboardTabs, PeriodSelector, type TabType } from '../components/LeaderboardTabs';
import { LeaderboardTable } from '../components/LeaderboardTable';
import { PlayerStreakCard } from '../components/PlayerStreakCard';
import {
  getGlobalLeaderboard,
  getWinsLeaderboard,
  getStreakLeaderboard,
  getPlayerStreak,
} from '../services/leaderboardApi';
import type {
  GlobalLeaderboardResponse,
  WinsLeaderboardResponse,
  StreakLeaderboardResponse,
  PlayerStreakDto,
} from '../types/leaderboard';
import './LeaderboardPage.css';

export function LeaderboardPage() {
  const { user } = useAuth();
  const [activeTab, setActiveTab] = useState<TabType>('global');
  const [selectedPeriod, setSelectedPeriod] = useState(() => {
    const now = new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    return `MONTHLY|${year}-${month}`;
  });

  const [globalData, setGlobalData] = useState<GlobalLeaderboardResponse | null>(null);
  const [winsData, setWinsData] = useState<WinsLeaderboardResponse | null>(null);
  const [streaksData, setStreaksData] = useState<StreakLeaderboardResponse | null>(null);
  const [playerStreak, setPlayerStreak] = useState<PlayerStreakDto | null>(null);

  const [loading, setLoading] = useState(false);
  const [streakLoading, setStreakLoading] = useState(false);

  const [error, setError] = useState<string | null>(null);
  const [streakError, setStreakError] = useState<string | null>(null);

  useEffect(() => {
    if (!user?.playerId) return;

    const fetchPlayerStreak = async () => {
      setStreakLoading(true);
      setStreakError(null);
      try {
        const data = await getPlayerStreak(user.playerId);
        setPlayerStreak(data);
      } catch (err) {
        setStreakError(err instanceof Error ? err.message : 'Failed to load streak');
      } finally {
        setStreakLoading(false);
      }
    };

    fetchPlayerStreak();
  }, [user?.playerId]);

  useEffect(() => {
    const fetchData = async () => {
      setLoading(true);
      setError(null);

      try {
        if (activeTab === 'global') {
          const [periodType, periodId] = selectedPeriod.split('|');
          const data = await getGlobalLeaderboard({
            periodType,
            periodId,
            limit: 20,
          });
          setGlobalData(data);
        } else if (activeTab === 'wins') {
          const data = await getWinsLeaderboard({
            category: 'most_wins',
            limit: 20,
          });
          setWinsData(data);
        } else if (activeTab === 'streaks') {
          const data = await getStreakLeaderboard({
            category: 'global_all_time',
            limit: 20,
          });
          setStreaksData(data);
        }
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load leaderboard');
      } finally {
        setLoading(false);
      }
    };

    fetchData();
  }, [activeTab, selectedPeriod]);

  const renderLeaderboard = () => {
    if (loading) {
      return <div className="loading">Loading leaderboard...</div>;
    }

    if (error) {
      return <div className="error">Error: {error}</div>;
    }

    if (activeTab === 'global' && globalData) {
      return (
        <>
          <PeriodSelector
            selectedPeriod={selectedPeriod}
            onPeriodChange={setSelectedPeriod}
          />
          <LeaderboardTable
            entries={globalData.entries}
            title={`${globalData.periodType} - ${globalData.periodId}`}
            scoreLabel="Points"
          />
        </>
      );
    }

    if (activeTab === 'wins' && winsData) {
      return (
        <LeaderboardTable
          entries={winsData.entries}
          title="Top Players by Wins"
          scoreLabel="Wins"
        />
      );
    }

    if (activeTab === 'streaks' && streaksData) {
      return (
        <LeaderboardTable
          entries={streaksData.entries}
          title="Longest Win Streaks"
          scoreLabel="Streak"
        />
      );
    }

    return null;
  };

  return (
    <div className="leaderboard-page">
      <div className="leaderboard-header">
        <h1>🏆 Leaderboards</h1>
        <Link to="/" className="back-button">
          ← Back to Game
        </Link>
      </div>

      <PlayerStreakCard
        streak={playerStreak}
        loading={streakLoading}
        error={streakError}
      />

      <LeaderboardTabs activeTab={activeTab} onTabChange={setActiveTab} />

      <div className="leaderboard-content">{renderLeaderboard()}</div>
    </div>
  );
}
