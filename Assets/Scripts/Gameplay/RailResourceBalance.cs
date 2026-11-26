using System;

/// <summary>Resource tuning in one place. See docs/resource-balance.md.</summary>
public static class RailResourceBalance
{
    public const string CrewKey = "rails.resourceCrew";
    public const float Capacity = 100f, StartingFuel = 50f;
    public const float StandardBlockMetres = 160f, FuelPerBlock = 50f;
    public const decimal ParticipationFactor = 0.6m;
    // November 24 (Justin): triple coal at a third of the value; total fuel per block is unchanged.
    public const int CoalTargetPerWorker = 12, CoalSpawnsPerWorker = 18;
    public const int GoldSpawnsPerWorker = 10, GoldBudgetPerBlock = 100;
    public static decimal Workers(int crew) => 1m + (Math.Clamp(crew, 1, 8) - 1) * ParticipationFactor;
    // November 26 (Justin): +10% per coal, then rounded up so every deposit adds a whole number.
    public const decimal CoalFuelBonus = 1.1m;
    // Decimal keeps exact results from ceiling one too high.
    public static float CoalValue(int crew) =>
        (float)Math.Ceiling((decimal)FuelPerBlock * CoalFuelBonus / (CoalTargetPerWorker * Workers(crew)));
    public static int CoalCount(int crew) => (int)(CoalSpawnsPerWorker * Workers(crew));
    public static int GoldCount(int crew) => (int)(GoldSpawnsPerWorker * Workers(crew));
    public const int MaxCrew = 8;
    /// <summary>Smallest crew whose per-block count includes selection index (prefix order).</summary>
    public static int CrewForResource(bool coal, int index)
    {
        for (int crew = 1; crew < MaxCrew; crew++)
            if ((coal ? CoalCount(crew) : GoldCount(crew)) > index) return crew;
        return MaxCrew;
    }
    // Integer wallet: carry the rational remainder across eligible deposits instead of rounding each bar.
    public static int GoldCredit(int denominator, int index) =>
        index * GoldBudgetPerBlock / denominator - (index - 1) * GoldBudgetPerBlock / denominator;
}
