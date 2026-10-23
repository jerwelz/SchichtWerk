namespace SchichtWerk.Domain;

/// <summary>
/// The interactive part of the demo: holds the baseline shift and the current scenario,
/// applies the two predefined changes and resets them. Every change produces a new,
/// validated scenario and a recalculated result; nothing outside can edit intervals or KPIs.
/// </summary>
public sealed class ShiftSimulation
{
    /// <summary>10 good parts become rejects: 5 in each of the two target intervals.</summary>
    public const int AdditionalRejectsPerInterval = 5;

    public ShiftSimulation()
    {
        Baseline = DemoScenarioFactory.CreateBaseline();
        BaselineResult = OeeCalculator.Calculate(Baseline);
        Current = Baseline;
        CurrentResult = BaselineResult;
    }

    public ShiftScenario Baseline { get; }

    public OeeResult BaselineResult { get; }

    public ShiftScenario Current { get; private set; }

    public OeeResult CurrentResult { get; private set; }

    public bool MaterialShortageApplied { get; private set; }

    public bool AdditionalRejectsApplied { get; private set; }

    public bool HasChanges => MaterialShortageApplied || AdditionalRejectsApplied;

    /// <summary>How the current scenario differs from the baseline.</summary>
    public ResultComparison ChangeFromBaseline => ResultComparison.Between(BaselineResult, CurrentResult);

    /// <summary>The machine stops for 10 more minutes (11:30–11:40) because material is missing.</summary>
    public void ApplyMaterialShortage()
    {
        if (MaterialShortageApplied)
            throw new InvalidOperationException("The material shortage has already been applied.");

        Update(Current.WithChangedIntervals(
            DemoScenarioFactory.MaterialShortageActionIntervals,
            interval => interval.ToDowntime(DowntimeReason.MaterialShortage)));
        MaterialShortageApplied = true;
    }

    /// <summary>10 parts produced 09:00–09:10 turn out to be defective. Output time and quantity stay the same.</summary>
    public void ApplyAdditionalRejects()
    {
        if (AdditionalRejectsApplied)
            throw new InvalidOperationException("The additional rejects have already been applied.");

        Update(Current.WithChangedIntervals(
            DemoScenarioFactory.RejectActionIntervals,
            interval => interval.WithAdditionalRejects(AdditionalRejectsPerInterval)));
        AdditionalRejectsApplied = true;
    }

    /// <summary>Restores the original shift. The baseline is immutable, so it can be reused directly.</summary>
    public void Reset()
    {
        Current = Baseline;
        CurrentResult = BaselineResult;
        MaterialShortageApplied = false;
        AdditionalRejectsApplied = false;
    }

    private void Update(ShiftScenario scenario)
    {
        Current = scenario;
        CurrentResult = OeeCalculator.Calculate(scenario);
    }
}
