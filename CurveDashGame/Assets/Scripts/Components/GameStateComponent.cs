namespace STG.CurveDash
{
    public enum GameState
    {
        Title,
        Playing,
        GameOver,
        GameEnd
    }
    
    public struct GameStateComponent
    {
        public GameState State;
        public float GameOverTimer;
    }
}