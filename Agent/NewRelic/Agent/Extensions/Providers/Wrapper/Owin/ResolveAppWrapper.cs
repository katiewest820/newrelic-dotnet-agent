﻿using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Owin
{
	public class ResolveAppWrapper : IWrapper
	{
		public bool IsTransactionRequired => false;

		private Func<object, object> _getBuilder;
		public Func<object, object> GetBuilder => _getBuilder ?? ( _getBuilder = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>("Microsoft.Owin.Hosting",
				"Microsoft.Owin.Hosting.Engine.StartContext", "Builder"));

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			return new CanWrapResponse("ResolveAppWrapper".Equals(methodInfo.RequestedWrapperName));
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
		{
			var context = instrumentedMethodCall.MethodCall.MethodArguments[0];

			var app = GetBuilder(context);

			var method = app.GetType().GetMethod("Use");

			method.Invoke(app, new object[]
			{
				typeof(OwinStartupMiddleware), new object[] { agentWrapperApi }
			});

			return Delegates.NoOp;
		}
	}
}