import { createContext, useContext, useState, useEffect, type ReactNode } from 'react';
import type { User } from '../types/auth';
import * as authApi from '../services/authApi';

interface AuthContextType {
  user: User | null;
  isAuthenticated: boolean;
  authError: string | null;
  login: (email: string, password: string) => Promise<void>;
  register: (username: string, email: string, password: string) => Promise<void>;
  logout: () => void;
  updateUser: (username: string) => void;
}

const AuthContext = createContext<AuthContextType | null>(null);

const STORAGE_KEY = 'npp_user';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    return stored ? JSON.parse(stored) : null;
  });
  const [authError, setAuthError] = useState<string | null>(null);

  useEffect(() => {
    if (user) {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(user));
    } else {
      localStorage.removeItem(STORAGE_KEY);
    }
  }, [user]);

  const login = async (email: string, password: string) => {
    try {
      setAuthError(null);
      const loggedInUser = await authApi.login({ email, password });
      setUser(loggedInUser);
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Login failed';
      setAuthError(message);
      throw error;
    }
  };

  const register = async (username: string, email: string, password: string) => {
    try {
      setAuthError(null);
      const registeredUser = await authApi.register({ username, email, password });
      setUser(registeredUser);
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Registration failed';
      setAuthError(message);
      throw error;
    }
  };

  const logout = () => {
    setUser(null);
    localStorage.removeItem('jwt_token'); // Delete token
  };

  const updateUser = (username: string) => {
    setUser(prev => prev ? { ...prev, username } : prev);
  };

  return (
    <AuthContext.Provider value={{ user, isAuthenticated: !!user, authError, login, register, logout, updateUser }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
