import { Link } from 'react-router-dom'
import { useGameHub } from '../hooks/useGameHub'
import { useAuth } from '../contexts/AuthContext'
import { Lobby } from '../components/Lobby'
import { GameCanvas } from '../components/GameCanvas'


export function GamePage() {
  const { user, logout } = useAuth();
  const token = localStorage.getItem('jwt_token');

  const {
    connected,
    lobby,
    gameState,
    appState,
    winner,
    isPlayer1,
    pauseMessage,
    createGame,
    joinGame,
    movePaddle,
    refreshLobby,
    returnToLobby,
    cancelGame,
  } = useGameHub(token);

  const playerName = user?.username ?? 'Player';
  const playerId = user?.playerId ?? '';

  if (!token || !user) {
    return (
      <div className="auth-required">
        <h1>Authentication Required</h1>
        <p>Please log in to play the game.</p>
        <Link to="/login">Go to Login</Link>
      </div>
    );
  }

  if (appState === 'reconnecting') {
    return (
      <div className="reconnecting">
        <h1>Reconnecting...</h1>
        <p>Attempting to reconnect to your game</p>
        <div className="loader"></div>
      </div>
    );
  }

  if (appState === 'lobby') {
    return (
      <Lobby
        lobby={lobby}
        connected={connected}
        playerName={playerName}
        onCreateGame={() => createGame(playerId, playerName)}
        onJoinGame={(gameId) => joinGame(gameId, playerId, playerName)}
        onRefresh={refreshLobby}
        onLogout={logout}
      />
    );
  }

  if (appState === 'waiting') {
    return (
      <div className="waiting">
        <h1>Waiting for opponent...</h1>
        <p>Share this page with a friend to play!</p>
        <div className="loader"></div>
        <button onClick={cancelGame} className="cancel-btn">
          Cancel
        </button>
      </div>
    );
  }

  if (appState === 'ended') {
    return (
      <div className="game-over">
        <h1>Game Over!</h1>
        <p className="winner">{winner} wins!</p>
        <div className="game-over-actions">
          <button onClick={returnToLobby}>Back to Lobby</button>
          <Link to="/leaderboard" className="secondary-btn">
            View Leaderboard
          </Link>
        </div>
      </div>
    );
  }

  if (appState === 'paused' && gameState) {
    return (
      <div className="paused-game">
        <GameCanvas
          gameState={gameState}
          onMovePaddle={movePaddle}
          isPlayer1={isPlayer1}
        />
        <div className="pause-overlay">
          <div className="pause-modal">
            <h2>Game Paused</h2>
            <p>{pauseMessage || 'Waiting for opponent to reconnect...'}</p>
            <div className="loader"></div>
            <button onClick={returnToLobby} className="leave-btn">
              Leave Game
            </button>
          </div>
        </div>
      </div>
    );
  }

  if (appState === 'playing' && gameState) {
    return (
      <GameCanvas
        gameState={gameState}
        onMovePaddle={movePaddle}
        isPlayer1={isPlayer1}
      />
    );
  }

  return <div>Loading...</div>;
}
