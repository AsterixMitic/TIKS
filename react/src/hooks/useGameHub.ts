import { useState, useEffect, useCallback, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import { saveReconnectToken, getReconnectToken, clearReconnectToken } from '../utils/reconnectStorage';

export interface Player {
  name: string;
  score: number;
}

export interface GameState {
  gameId: string;
  ball: { x: number; y: number };
  paddle1: { y: number };
  paddle2: { y: number };
  player1: Player | null;
  player2: Player | null;
  state: string;
}

export interface LobbyGame {
  gameId: string;
  hostName: string;
}

export interface ReconnectTokenData {
  token: string;
  gameId: string;
  playerNumber: number;
}

export type AppState = 'lobby' | 'waiting' | 'playing' | 'paused' | 'reconnecting' | 'ended';

export function useGameHub(authToken: string | null) {
  const [, setConnection] = useState<signalR.HubConnection | null>(null);
  const [connected, setConnected] = useState(false);
  const [lobby, setLobby] = useState<LobbyGame[]>([]);
  const [gameState, setGameState] = useState<GameState | null>(null);
  const [appState, setAppState] = useState<AppState>('lobby');
  const [winner, setWinner] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isPlayer1, setIsPlayer1] = useState(true);
  const [pauseMessage, setPauseMessage] = useState<string | null>(null);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  useEffect(() => {
    if (!authToken) {
      setConnected(false);
      return;
    }

    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl('/gamehub', {
        accessTokenFactory: () => authToken
      })
      .withAutomaticReconnect()
      .build();

    newConnection.on('LobbyUpdated', (games: LobbyGame[]) => {
      setLobby(games);
    });

    newConnection.on('GameStarted', (state: GameState) => {
      setGameState(state);
      setAppState('playing');
      setPauseMessage(null);
    });

    newConnection.on('GameStateUpdated', (state: GameState) => {
      setGameState(state);
    });

    newConnection.on('GameEnded', (winnerName: string) => {
      setWinner(winnerName);
      setAppState('ended');
      clearReconnectToken();
    });

    newConnection.on('JoinFailed', (message: string) => {
      setError(message);
    });

    newConnection.on('ReconnectToken', (data: ReconnectTokenData) => {
      saveReconnectToken(data);
    });

    newConnection.on('PendingGameFound', (data: { gameId: string; playerNumber: number; playerName: string }) => {
      setAppState('reconnecting');
      setIsPlayer1(data.playerNumber === 1);
      const token = getReconnectToken();
      if (token) {
        newConnection.invoke('Reconnect', token.token);
      }
    });

    newConnection.on('NoPendingGame', () => {
      clearReconnectToken();
      setAppState('lobby');
    });

    newConnection.on('Reconnected', (data: {
      success: boolean;
      gameId: string;
      playerNumber: number;
      playerName: string;
      gameState: GameState;
    }) => {
      if (data.success) {
        setGameState(data.gameState);
        setIsPlayer1(data.playerNumber === 1);

        if (data.gameState.state === 'Paused') {
          setAppState('paused');
          setPauseMessage('Waiting for opponent to reconnect...');
        } else {
          setAppState('playing');
          setPauseMessage(null);
        }
      }
    });

    newConnection.on('ReconnectFailed', (message: string) => {
      clearReconnectToken();
      setAppState('lobby');
      setError(message);
    });

    newConnection.on('OpponentDisconnected', () => {
      setPauseMessage('Opponent disconnected. Waiting for them to reconnect...');
    });

    newConnection.on('GamePaused', () => {
      setAppState('paused');
      setPauseMessage('Game paused. Waiting for opponent to reconnect...');
    });

    newConnection.on('GameResumed', (state: GameState) => {
      setGameState(state);
      setAppState('playing');
      setPauseMessage(null);
    });

    newConnection.on('GameCancelled', () => {
      clearReconnectToken();
      setAppState('lobby');
      setGameState(null);
      setError(null);
    });

    newConnection.on('CancelFailed', (message: string) => {
      setError(message);
    });

    newConnection.start()
      .then(() => {
        setConnected(true);

        const storedToken = getReconnectToken();
        if (storedToken) {
          setAppState('reconnecting');
          newConnection.invoke('CheckPendingGame', storedToken.token);
        } else {
          newConnection.invoke('GetLobby');
        }
      })
      .catch(err => console.error('Connection failed:', err));

    setConnection(newConnection);
    connectionRef.current = newConnection;

    return () => {
      newConnection.stop();
    };
  }, [authToken]);

  useEffect(() => {
    const handleBeforeUnload = (e: BeforeUnloadEvent) => {
      e.preventDefault();
      e.returnValue = '';
      return '';
    };

    const isInActiveGame = appState === 'playing' || appState === 'waiting' || appState === 'paused';

    if (isInActiveGame) {
      window.addEventListener('beforeunload', handleBeforeUnload);
    }

    return () => {
      window.removeEventListener('beforeunload', handleBeforeUnload);
    };
  }, [appState]);

  const createGame = useCallback(async (playerId: string, playerName: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      setIsPlayer1(true);
      await connectionRef.current.invoke('CreateGame', playerName);
      setAppState('waiting');
    }
  }, []);

  const joinGame = useCallback(async (gameId: string, playerId: string, playerName: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      setIsPlayer1(false);
      await connectionRef.current.invoke('JoinGame', gameId, playerName);
    }
  }, []);

  const movePaddle = useCallback((y: number) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      connectionRef.current.invoke('MovePaddle', y);
    }
  }, []);

  const refreshLobby = useCallback(async () => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('GetLobby');
    }
  }, []);

  const returnToLobby = useCallback(() => {
    setAppState('lobby');
    setGameState(null);
    setWinner(null);
    setError(null);
    setPauseMessage(null);
    clearReconnectToken();
    refreshLobby();
  }, [refreshLobby]);

  const attemptReconnect = useCallback(async () => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      const storedToken = getReconnectToken();
      if (storedToken) {
        setAppState('reconnecting');
        await connectionRef.current.invoke('Reconnect', storedToken.token);
      }
    }
  }, []);

  const cancelGame = useCallback(async () => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('CancelGame');
    }
  }, []);

  return {
    connected,
    lobby,
    gameState,
    appState,
    winner,
    error,
    isPlayer1,
    pauseMessage,
    createGame,
    joinGame,
    movePaddle,
    refreshLobby,
    returnToLobby,
    attemptReconnect,
    cancelGame,
  };
}
