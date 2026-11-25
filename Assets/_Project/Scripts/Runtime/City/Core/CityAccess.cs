namespace ZombieCity.City.Core
{
    /// <summary>
    /// Derived access class per cell (ADR-003 §2).
    /// </summary>
    public enum CityAccess : byte
    {
        Obstacle = 0,
        Roadway = 1,
        Pedestrian = 2
    }
}
