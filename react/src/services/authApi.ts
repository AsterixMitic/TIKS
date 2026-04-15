import type { User, LoginRequest, RegisterRequest } from '../types/auth';

const API_BASE = '/auth';

export async function login(request: LoginRequest): Promise<User> {
  const response = await fetch(`${API_BASE}/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    const contentType = response.headers.get('content-type');
    if (contentType?.includes('application/json')) {
      const errorData = await response.json();
      throw new Error(errorData.detail || 'Login failed');
    }
    const error = await response.text();
    throw new Error(error || 'Login failed');
  }

  const data = await response.json();
  localStorage.setItem('jwt_token', data.token);
  return data;
}

export async function register(request: RegisterRequest): Promise<User> {
  const response = await fetch(`${API_BASE}/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    const contentType = response.headers.get('content-type');
    if (contentType?.includes('application/json')) {
      const errorData = await response.json();
      throw new Error(errorData.detail || 'Registration failed');
    }
    const error = await response.text();
    throw new Error(error || 'Registration failed');
  }

  const data = await response.json();
  localStorage.setItem('jwt_token', data.token);
  return data;
}
