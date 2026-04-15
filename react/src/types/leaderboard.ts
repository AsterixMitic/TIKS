export interface LeaderboardEntry {
  rank: number;
  playerId: string;
  username: string;
  score: number;
}

export interface GlobalLeaderboardResponse {
  periodType: string;
  periodId: string;
  entries: LeaderboardEntry[];
}

export interface WinsLeaderboardResponse {
  category: string;
  entries: LeaderboardEntry[];
}

export interface StreakLeaderboardResponse {
  category: string;
  entries: LeaderboardEntry[];
}

export interface PlayerStreakDto {
  playerId: string;
  username: string;
  currentStreak: number;
  longestStreak: number;
  lastResult: string;
}

export interface GlobalLeaderboardRequest {
  periodType: string;
  periodId: string;
  limit?: number;
}

export interface WinsLeaderboardRequest {
  category?: string;
  limit?: number;
}

export interface StreakLeaderboardRequest {
  category?: string;
  limit?: number;
}

export type PeriodType = 'MONTHLY' | 'YEARLY' | 'ALL_TIME';
