using System;
using System.Collections.Generic;
using UnityEngine;

// Base Module class - can be extended for various functionalities
[Serializable]
/// <summary>
/// Handles module.
/// </summary>
public class Module
{
    public string ModuleName;
    public Module(string moduleName)
    {
        ModuleName = moduleName;
    }
}
