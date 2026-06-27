using System;
using System.Collections.Generic;

namespace DistributedSystem.Saga;

public sealed record SagaStep(string Name, Func<bool> Execute, Func<bool> Compensate);

public sealed class Saga
{
    private readonly List<SagaStep> _steps = [];

    public void AddStep(string name, Func<bool> execute, Func<bool> compensate)
    {
        _steps.Add(new SagaStep(name, execute, compensate));
    }

    public bool Run()
    {
        var executed = new List<string>();

        foreach (var step in _steps)
        {
            var succeeded = step.Execute();
            if (!succeeded)
            {
                Console.WriteLine($"Step failed: {step.Name}");
                Compensate(executed);
                return false;
            }

            executed.Add(step.Name);
        }

        return true;
    }

    private static void Compensate(IEnumerable<string> executedSteps)
    {
        foreach (var stepName in executedSteps.Reverse())
        {
            Console.WriteLine($"Compensating: {stepName}");
        }
    }
}
