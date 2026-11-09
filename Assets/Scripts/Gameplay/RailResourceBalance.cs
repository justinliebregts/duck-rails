using System;

/// <summary>Resource tuning in one place. See docs/resource-balance.md.</summary>
public static class RailResourceBalance
{
    public const string CrewKey = "rails.resourceCrew";
    public const float Capacity = 100f, StartingFuel = 50f;
    public const float StandardBlockMetres = 160f, FuelPerBlock = 50f;
    public const decimal ParticipationFactor = 0.6m;
    public const int CoalTargetPerWorker = 4, CoalSpawnsPerWorker = 6;
    public const int GoldSpawnsPerWorker = 10, GoldBudgetPerBlock = 100;
    public static decimal Workers(int crew) => 1m + (Math.Clamp(crew, 1, 8) - 1) * ParticipationFactor;
    public static float CoalValue(int crew) => FuelPerBlock / (CoalTargetPerWorker * (float)Workers(crew));
    public static int CoalCount(int crew) => (int)(CoalSpawnsPerWorker * Workers(crew));
    public static int GoldCount(int crew) => (int)(GoldSpawnsPerWorker * Workers(crew));
    // Integer wallet: carry the rational remainder across eligible deposits instead of rounding each bar.
    public static int GoldCredit(int denominator, int index) =>
        index * GoldBudgetPerBlock / denominator - (index - 1) * GoldBudgetPerBlock / denominator;
}
