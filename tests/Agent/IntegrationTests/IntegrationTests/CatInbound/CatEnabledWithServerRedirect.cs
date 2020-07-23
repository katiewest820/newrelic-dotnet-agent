﻿using System.Linq;
using System.Net.Http.Headers;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CatInbound
{
    public class CatEnabledWithServerRedirect : IClassFixture<RemoteServiceFixtures.BasicMvcApplication>
	{
		[NotNull]
		private RemoteServiceFixtures.BasicMvcApplication _fixture;

		[NotNull]
		private HttpResponseHeaders _responseHeaders;

		public CatEnabledWithServerRedirect([NotNull] RemoteServiceFixtures.BasicMvcApplication fixture, [NotNull] ITestOutputHelper output)
		{
			_fixture = fixture;
			_fixture.TestLogger = output;
			_fixture.Actions
			(
				setupConfiguration: () =>
				{
					var configPath = fixture.DestinationNewRelicConfigFilePath;
					var configModifier = new NewRelicConfigModifier(configPath);

					configModifier.ForceTransactionTraces();
					
					CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_fixture.DestinationNewRelicConfigFilePath, new[] { "configuration" }, "crossApplicationTracingEnabled", "true");
					CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_fixture.DestinationNewRelicConfigFilePath, new[] { "configuration", "crossApplicationTracer" }, "enabled", "true");
				},
				exerciseApplication: () =>
				{
					_fixture.GetIgnored();
					_responseHeaders = _fixture.GetWithCatHeaderWithRedirect();
				}
			);
			_fixture.Initialize();
		}

		[Fact]
		public void Test()
		{
			var catResponseHeader = _responseHeaders.GetValues(@"X-NewRelic-App-Data")?.FirstOrDefault();
			Assert.NotNull(catResponseHeader);

			var catResponseData = HeaderEncoder.DecodeAndDeserialize<CrossApplicationResponseData>(catResponseHeader, HeaderEncoder.IntegrationTestEncodingKey);
			
			var transactionEventIndex = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/DefaultController/Index");
			var transactionEventRedirect = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/DefaultController/DoRedirect");
			var transactionSampleTxEvent = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/MVC/DefaultController/Index");
			var transactionSampleTxEventRedirect = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/MVC/DefaultController/DoRedirect");
			// cannot predict which tx will be the longest
			var transactionSample = transactionSampleTxEvent != null ? transactionSampleTxEvent : transactionSampleTxEventRedirect;
			var metrics = _fixture.AgentLog.GetMetrics();

			NrAssert.Multiple
			(
				() => Assert.NotNull(transactionSample),
				() => Assert.NotNull(transactionEventRedirect),
				() => Assert.NotNull(transactionEventIndex)
			);

			NrAssert.Multiple
			(
				() => Assert.Equal($"{_fixture.AgentLog.GetAccountId()}#{_fixture.AgentLog.GetApplicationId()}", catResponseData.CrossProcessId),
				() => Assert.Equal("WebTransaction/MVC/DefaultController/Index", catResponseData.TransactionName),
				() => Assert.True(catResponseData.QueueTimeInSeconds >= 0),
				() => Assert.True(catResponseData.ResponseTimeInSeconds >= 0),
				() => Assert.Equal(-1, catResponseData.ContentLength),
				() => Assert.NotNull(catResponseData.TransactionGuid),
				() => Assert.Equal(false, catResponseData.Unused),
				
				// Trace attributes
				() => Assertions.TransactionTraceHasAttributes(Expectations.ExpectedTransactionTraceIntrinsicAttributesCatEnabled, TransactionTraceAttributeType.Intrinsic, transactionSample),
				() => Assertions.TransactionTraceDoesNotHaveAttributes(Expectations.UnexpectedTransactionTraceIntrinsicAttributesCatEnabled, TransactionTraceAttributeType.Intrinsic, transactionSample),

				// transactionEventIndex attributes
				() => Assertions.TransactionEventHasAttributes(Expectations.ExpectedTransactionEventIntrinsicAttributesCatEnabled, TransactionEventAttributeType.Intrinsic, transactionEventIndex),
				() => Assertions.TransactionEventDoesNotHaveAttributes(Expectations.UnexpectedTransactionEventIntrinsicAttributesCatEnabled, TransactionEventAttributeType.Intrinsic, transactionEventIndex),

				// transactionEventRedirect attributes
				() => Assertions.TransactionEventHasAttributes(Expectations.ExpectedTransactionEventIntrinsicAttributesCatEnabled, TransactionEventAttributeType.Intrinsic, transactionEventRedirect),
				() => Assertions.TransactionEventDoesNotHaveAttributes(Expectations.UnexpectedTransactionEventIntrinsicAttributesCatEnabled, TransactionEventAttributeType.Intrinsic, transactionEventRedirect),

				() => Assertions.MetricsExist(Expectations.ExpectedMetricsCatEnabled, metrics)
			);
		}
	}
}
