﻿namespace Bones
{
    using System;

    public class Transient : ILifeSpan
    {
        public object Resolve(IAdvancedScope currentScope, Contract contract)
        {
            var instance = new Instance()
            {
                Value = contract.CreateInstance(currentScope),
                Contract = contract
            };
            
            currentScope.Tracked.Push(instance);
            
            return instance.Value;
        }
        
        
    }
}