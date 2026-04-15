import type { MatchHistory, PlayerMatches, PlayerMatchesRequest, PlayerMatchesResponse } from "../types/playerMatches";

const API_BASE = "api/playermatches"


export async function getMatchesByYear(year: string, page: number = 1, limit: number = 5): Promise<PlayerMatchesResponse[]> {

    const storedUser = localStorage.getItem('npp_user');
    let token = '';


    if (storedUser) {
        const userObject = JSON.parse(storedUser);
        token = userObject.token;
    }

    const response = await fetch(`${API_BASE}/${year}?page=${page}&limit=${limit}`, {
        method: 'GET',
        headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${token}`
        }
    });
    if (!response.ok) {
        const error = await response.text();
        throw new Error(error || 'PlayerMatchesByYear get failed');
    }

    return response.json();
}

export async function deleteMatch(year: string, matchId: string): Promise<void> {
    const storedUser = localStorage.getItem('npp_user');
    let token = '';

    if (storedUser) {
        const userObject = JSON.parse(storedUser);
        token = userObject.token;
    }

    const response = await fetch(`${API_BASE}/${year}/${matchId}`, {
        method: 'DELETE',
        headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${token}`
        }
    });

    if (!response.ok) {
        const error = await response.text();
        throw new Error(error || 'Delete match failed');
    }
}

export async function GetHistory(page: number = 1, limit: number = 10) : Promise <MatchHistory[]> {
    const storedUser = localStorage.getItem('npp_user');
    let token = '';


    if (storedUser) {
        const userObject = JSON.parse(storedUser);
        token = userObject.token;
    }

    const response = await fetch(`${API_BASE}/history?page=${page}&limit=${limit}`,
        {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            }
        }
    );

    if (!response.ok) {
        const error = await response.text();
        throw new Error(error || 'MatchHistory get failed');
    }

    return response.json();
}