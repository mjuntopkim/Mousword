using UnityEngine;

public class FSM
{
    public FSM(BaseState initialState)
    {
        curState = initialState;
        changeState(curState);
    }

    protected BaseState curState;

    public void changeState(BaseState newState)
    {
        if (newState == curState) return;

       //if (curState != null) { curState.stateExit(); Debug.Log("State exited: " + curState.GetType().Name); }

        curState = newState;
        curState.stateEnter();
    }

    public void stateUpdate()
    {
        if (curState != null) curState.stateUpdate();
    }
}
