import { useRef, useEffect, useCallback } from 'react';
import type { GameState } from '../hooks/useGameHub';

const CANVAS_WIDTH = 800;
const CANVAS_HEIGHT = 600;
const PADDLE_WIDTH = 10;
const PADDLE_HEIGHT = 100;
const PADDLE_OFFSET = 20;
const BALL_SIZE = 10;

interface GameCanvasProps {
  gameState: GameState;
  onMovePaddle: (y: number) => void;
  isPlayer1: boolean;
}

export function GameCanvas({ gameState, onMovePaddle, isPlayer1 }: GameCanvasProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const keysPressed = useRef<Set<string>>(new Set());
  const paddleY = useRef(CANVAS_HEIGHT / 2 - PADDLE_HEIGHT / 2);

  const draw = useCallback(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    ctx.fillStyle = '#1a1a2e';
    ctx.fillRect(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);

    ctx.strokeStyle = '#444';
    ctx.setLineDash([10, 10]);
    ctx.beginPath();
    ctx.moveTo(CANVAS_WIDTH / 2, 0);
    ctx.lineTo(CANVAS_WIDTH / 2, CANVAS_HEIGHT);
    ctx.stroke();
    ctx.setLineDash([]);

    ctx.fillStyle = '#fff';
    ctx.fillRect(PADDLE_OFFSET, gameState.paddle1.y, PADDLE_WIDTH, PADDLE_HEIGHT);
    ctx.fillRect(CANVAS_WIDTH - PADDLE_OFFSET - PADDLE_WIDTH, gameState.paddle2.y, PADDLE_WIDTH, PADDLE_HEIGHT);

    ctx.fillStyle = '#ff6b6b';
    ctx.fillRect(gameState.ball.x, gameState.ball.y, BALL_SIZE, BALL_SIZE);

    ctx.fillStyle = '#fff';
    ctx.font = '48px monospace';
    ctx.textAlign = 'center';
    ctx.fillText(String(gameState.player1?.score ?? 0), CANVAS_WIDTH / 4, 60);
    ctx.fillText(String(gameState.player2?.score ?? 0), (CANVAS_WIDTH * 3) / 4, 60);

    ctx.font = '16px monospace';
    ctx.fillText(gameState.player1?.name ?? 'Player 1', CANVAS_WIDTH / 4, 90);
    ctx.fillText(gameState.player2?.name ?? 'Player 2', (CANVAS_WIDTH * 3) / 4, 90);
  }, [gameState]);

  useEffect(() => {
    draw();
  }, [draw]);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (['w', 's', 'ArrowUp', 'ArrowDown'].includes(e.key)) {
        e.preventDefault();
        keysPressed.current.add(e.key);
      }
    };

    const handleKeyUp = (e: KeyboardEvent) => {
      keysPressed.current.delete(e.key);
    };

    window.addEventListener('keydown', handleKeyDown);
    window.addEventListener('keyup', handleKeyUp);

    const moveInterval = setInterval(() => {
      const speed = 8;
      let moved = false;

      if (keysPressed.current.has('w') || keysPressed.current.has('ArrowUp')) {
        paddleY.current = Math.max(0, paddleY.current - speed);
        moved = true;
      }
      if (keysPressed.current.has('s') || keysPressed.current.has('ArrowDown')) {
        paddleY.current = Math.min(CANVAS_HEIGHT - PADDLE_HEIGHT, paddleY.current + speed);
        moved = true;
      }

      if (moved) {
        onMovePaddle(paddleY.current);
      }
    }, 1000 / 60);

    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      window.removeEventListener('keyup', handleKeyUp);
      clearInterval(moveInterval);
    };
  }, [onMovePaddle]);

  useEffect(() => {
    paddleY.current = isPlayer1 ? gameState.paddle1.y : gameState.paddle2.y;
  }, [isPlayer1, gameState.paddle1.y, gameState.paddle2.y]);

  return (
    <div className="game-container">
      <div className="game-info">
        You are: {isPlayer1 ? 'Player 1 (Left)' : 'Player 2 (Right)'} | Use W/S or Arrow keys to move
      </div>
      <canvas
        ref={canvasRef}
        width={CANVAS_WIDTH}
        height={CANVAS_HEIGHT}
        tabIndex={0}
      />
    </div>
  );
}
