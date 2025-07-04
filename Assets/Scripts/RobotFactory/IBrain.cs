using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IBrain
{
    void Update();
    void HandleStateChange(RobotState newState);
}