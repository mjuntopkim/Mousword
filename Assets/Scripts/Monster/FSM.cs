using UnityEngine;

public class FSM
{
    // ===== [원본 코드 - 문제 있음] =====
    // 같은 타입으로 changeState를 호출하면 바로 return되어 첫 상태의 stateEnter()가 실행되지 않음
    //public FSM(BaseState initialState)
    //{
    //    curState = initialState;
    //    changeState(curState);
    //}
    // ===== [원본 코드 끝] =====

    public FSM(BaseState initialState)
    {
        curState = initialState;
        curState.stateEnter(); //첫 상태의 stateEnter를 직접 실행
    }

    protected BaseState curState;

    public void changeState(BaseState newState)
    {
        if (newState.GetType() == curState.GetType()) { return; }

        if (curState != null) {curState.stateExit();}

        curState = newState;
        curState.stateEnter();
    }

    public void stateUpdate()
    {
        if (curState != null) curState.stateUpdate();
    }
}
