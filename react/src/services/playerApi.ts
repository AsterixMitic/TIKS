export interface PlayerStats {
  totalPoints: number;
  wins: number;
  losses: number;
  winRate: string;
}

export interface PlayerProfile {
  playerId: string;
  username: string;
  email: string;
  avatarUrl: string | null;
  createdAt: string;
}

export interface UpdatePlayerRequest {
  username?: string;
  avatarUrl?: string;
}

function getAuthHeaders(): HeadersInit {
  const token = localStorage.getItem('jwt_token');
  return {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${token}`
  };
}

export async function getMyStats(): Promise<PlayerStats> {
  const response = await fetch('/player/me/stats', {
    method: 'GET',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch player stats');
  }

  return response.json();
}

export async function getMyProfile(): Promise<PlayerProfile> {
  const response = await fetch('/player/me', {
    method: 'GET',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch player profile');
  }

  return response.json();
}

export async function updateMyProfile(data: UpdatePlayerRequest): Promise<PlayerProfile> {
  const response = await fetch('/player/me', {
    method: 'PUT',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    const error = await response.text();
    throw new Error(error || 'Failed to update player profile');
  }

  return response.json();
}

export async function deleteMyAccount(): Promise<void> {
  const response = await fetch('/player/me', {
    method: 'DELETE',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to delete account');
  }
}
