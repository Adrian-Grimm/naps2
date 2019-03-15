﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace NAPS2.Config.Experimental
{
    public class ObjectConfigScope<TConfig> : ConfigScope<TConfig> where TConfig : new()
    {
        private readonly TConfig obj;

        public ObjectConfigScope(TConfig obj, ConfigScopeMode mode) : base(mode)
        {
            this.obj = obj;
        }

        protected override T GetInternal<T>(Func<TConfig, T> func) => func(obj);

        protected override void SetInternal(Action<TConfig> func) => func(obj);

        public override void SetAllInternal(TConfig delta)
        {
            ConfigCopier.Copy(delta, obj);
        }
    }
}
