import './UserProfile.css';
import { getMatchesByYear } from '../services/playerMatchesApi';
import { deleteMatch } from '../services/playerMatchesApi';
import { getMyStats, updateMyProfile, deleteMyAccount, type PlayerStats } from '../services/playerApi';
import { useState, useEffect } from 'react';
import { useAuth } from '../contexts/AuthContext';
import type { PlayerMatchesResponse } from '../types/playerMatches';

const UserProfile = () => {
  const { user, logout, updateUser } = useAuth();
  const [matches, setMatches] = useState<PlayerMatchesResponse[]>([]);
  const [stats, setStats] = useState<PlayerStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [statsLoading, setStatsLoading] = useState(true);

  const [isEditing, setIsEditing] = useState(false);
  const [editUsername, setEditUsername] = useState('');
  const [editError, setEditError] = useState('');
  const [editLoading, setEditLoading] = useState(false);

  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleteLoading, setDeleteLoading] = useState(false);

  const currentYear = new Date().getFullYear().toString();
  const username = user?.username ?? 'Player';
  const avatarUrl = `https://api.dicebear.com/7.x/avataaars/svg?seed=${username}`;

  useEffect(() => {
    getMyStats()
      .then((data) => {
        setStats(data);
        setStatsLoading(false);
      })
      .catch((error) => {
        console.error("Error loading stats:", error);
        setStatsLoading(false);
      });

    getMatchesByYear(currentYear, 1, 5)
      .then((data) => {
        setMatches(data);
        setLoading(false);
      })
      .catch((error) => {
        console.error("Error loading matches:", error);
        setLoading(false);
      });
  }, []);

  const handleEditOpen = () => {
    setEditUsername(username);
    setEditError('');
    setIsEditing(true);
  };

  const handleEditSave = async () => {
    if (!editUsername.trim()) return;
    setEditLoading(true);
    setEditError('');
    try {
      await updateMyProfile({ username: editUsername.trim() });
      updateUser(editUsername.trim());
      setIsEditing(false);
    } catch (err) {
      setEditError(err instanceof Error ? err.message : 'Update failed');
    } finally {
      setEditLoading(false);
    }
  };

  const handleDeleteAccount = async () => {
    setDeleteLoading(true);
    try {
      await deleteMyAccount();
      logout();
    } catch (err) {
      console.error('Delete account failed:', err);
      setDeleteLoading(false);
      setShowDeleteConfirm(false);
    }
  };

  const handleDeleteMatch = async (matchId: string) => {
    try {
      await deleteMatch(currentYear, matchId);
      setMatches(prev => prev.filter(m => m.matchId !== matchId));
    } catch (err) {
      console.error('Delete match failed:', err);
    }
  };

  return (
    <div className="profile-widget">
      <div className="profile-header">
        <img
          src={avatarUrl}
          alt="User Avatar"
          className="profile-avatar"
        />
        <div className="profile-info">
          <h3 className="profile-name">{username}</h3>
          <span className="profile-status">Online</span>
        </div>
      </div>

      {isEditing ? (
        <div className="edit-profile-form">
          <input
            id="edit-username"
            type="text"
            value={editUsername}
            onChange={e => setEditUsername(e.target.value)}
            placeholder="New username"
          />
          {editError && <p className="profile-error">{editError}</p>}
          <div className="edit-profile-buttons">
            <button id="save-profile-btn" onClick={handleEditSave} disabled={editLoading}>
              {editLoading ? 'Saving...' : 'Save'}
            </button>
            <button id="cancel-edit-btn" onClick={() => setIsEditing(false)} disabled={editLoading}>
              Cancel
            </button>
          </div>
        </div>
      ) : (
        <div className="profile-actions">
          <button className="edit-profile-btn" onClick={handleEditOpen}>Edit Profile</button>
          {!showDeleteConfirm ? (
            <button id="delete-account-btn" className="delete-account-btn" onClick={() => setShowDeleteConfirm(true)}>
              Delete Account
            </button>
          ) : (
            <div className="confirm-delete-section">
              <p>Are you sure? This cannot be undone.</p>
              <button id="confirm-delete-btn" className="confirm-delete-btn" onClick={handleDeleteAccount} disabled={deleteLoading}>
                {deleteLoading ? 'Deleting...' : 'Confirm Delete'}
              </button>
              <button onClick={() => setShowDeleteConfirm(false)} disabled={deleteLoading}>Cancel</button>
            </div>
          )}
        </div>
      )}

      <div className="profile-stats">
        {statsLoading ? (
          <p style={{textAlign: 'center', color: '#888', width: '100%'}}>Loading stats...</p>
        ) : (
          <>
            <div className="stat-item">
              <span className="stat-value">{stats?.wins ?? 0}</span>
              <span className="stat-label">Wins</span>
            </div>
            <div className="stat-item">
              <span className="stat-value">{stats?.losses ?? 0}</span>
              <span className="stat-label">Losses</span>
            </div>
            <div className="stat-item">
              <span className="stat-value highlight">{stats?.winRate ?? '0%'}</span>
              <span className="stat-label">Win Rate</span>
            </div>
          </>
        )}
      </div>

      <div className="matches-history">
        <h4>Last 5 Matches</h4>

        {loading ? (
          <p style={{textAlign: 'center', color: '#888'}}>Loading matches...</p>
        ) : (
          <ul className="match-list">
            {matches.map((match, index) => (
              <li
                key={`${match.matchId}-${index}`}
                className={`match-item ${match.result ? match.result.toLowerCase() : ''}`}
                data-match-id={match.matchId}
                data-match-year={currentYear}
              >
                <span className="match-result">{match.result}</span>
                <span className="match-score">{match.score}</span>
                <span className="match-map">{match.opponentUsername}</span>
                <button
                  className="delete-match-btn"
                  onClick={() => handleDeleteMatch(match.matchId)}
                  title="Delete match"
                >
                  ✕
                </button>
              </li>
            ))}

            {matches.length === 0 && <p style={{fontSize: '0.8rem', textAlign: 'center'}}>No matches found.</p>}
          </ul>
        )}
      </div>
    </div>
  );
};

export default UserProfile;
