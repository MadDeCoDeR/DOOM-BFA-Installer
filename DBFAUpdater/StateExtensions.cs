using System.Collections.Generic;

namespace DBFAUpdater;
public static class StateExtensions
{
    public static StateMachine? GetNextState(this List<StateMachine> states, StateMachine currentState)
    {
        StateMachine? resultState = null;
        foreach(StateMachine state in states)
        {
            if (state.Current == currentState.Next)
            {
                resultState = state;
                break;
            }
        }
        return resultState;
    }

    public static StateMachine? GetPreviousState(this List<StateMachine> states, StateMachine currentState)
    {
        StateMachine? resultState = null;
        foreach(StateMachine state in states)
        {
            if (state.Current == currentState.Previous)
            {
                resultState = state;
                break;
            }
        }
        return resultState;
    }
}