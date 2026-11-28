namespace Survival;

public enum IntakeKind { Grazed, Carcass, Remains, Store }

/// Where an animal's kcal come from, for the food chain bench: set on a world to read it, never saved.
internal interface IIntakeSink
{
    void Took(Agent animal, IntakeKind kind, double kcal, Cause? killedBy);
}
