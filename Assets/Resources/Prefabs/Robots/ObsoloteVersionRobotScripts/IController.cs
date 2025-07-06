using System;
using UnityEngine;
public interface IController
{
    void Move(float direction);
    void Jump();
    void Attack();
}