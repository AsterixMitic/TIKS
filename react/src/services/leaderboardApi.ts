import type {
  GlobalLeaderboardResponse,
  WinsLeaderboardResponse,
  StreakLeaderboardResponse,
  PlayerStreakDto,
  GlobalLeaderboardRequest,
  WinsLeaderboardRequest,
  StreakLeaderboardRequest,
} from '../types/leaderboard';

const API_BASE = '/api/leaderboard';

function getAuthHeaders(): HeadersInit {
  const token = localStorage.getItem('jwt_token');
  const headers: HeadersInit = {
    'Content-Type': 'application/json',
  };
  
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }
  
  return headers;
}

export async function getGlobalLeaderboard(
  params: GlobalLeaderboardRequest
): Promise<GlobalLeaderboardResponse> {
  const { periodType, periodId, limit = 10 } = params;
  const queryParams = new URLSearchParams({
    periodType,
    periodId,
    limit: limit.toString(),
  });

  const response = await fetch(`${API_BASE}/global?${queryParams}`);

  if (!response.ok) {
    throw new Error('Failed to fetch global leaderboard');
  }

  return response.json();
}

export async function getWinsLeaderboard(
  params?: WinsLeaderboardRequest
): Promise<WinsLeaderboardResponse> {
  const { category = 'most_wins', limit = 10 } = params || {};
  const queryParams = new URLSearchParams({
    category,
    limit: limit.toString(),
  });

  const response = await fetch(`${API_BASE}/wins?${queryParams}`);

  if (!response.ok) {
    throw new Error('Failed to fetch wins leaderboard');
  }

  return response.json();
}

export async function getPlayerStreak(playerId: string): Promise<PlayerStreakDto> {
  const response = await fetch(`${API_BASE}/streak/${playerId}`);

  if (!response.ok) {
    if (response.status === 404) {
      throw new Error('Player streak not found');
    }
    throw new Error('Failed to fetch player streak');
  }

  return response.json();
}

export async function getStreakLeaderboard(
  params?: StreakLeaderboardRequest
): Promise<StreakLeaderboardResponse> {
  const { category = 'global_all_time', limit = 10 } = params || {};
  const queryParams = new URLSearchParams({
    category,
    limit: limit.toString(),
  });

  const response = await fetch(`${API_BASE}/longest-streak?${queryParams}`);

  if (!response.ok) {
    throw new Error('Failed to fetch streak leaderboard');
  }

  return response.json();
}
