namespace GenericMatrixConnectionHandler
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.MediaOps.Live.API.Enums;
	using Skyline.DataMiner.MediaOps.Live.Automation.Mediation.ConnectionHandlers;
	using Skyline.DataMiner.MediaOps.Live.Mediation.Data;

	public class Script : ConnectionHandlerScript
	{
		public override IEnumerable<ElementInfo> GetSupportedElements(IEngine engine, IEnumerable<ElementInfo> elements)
		{
			return elements.Where(e => e.Protocol == "Generic Matrix");
		}

		public override IEnumerable<SubscriptionInfo> GetSubscriptionInfo(IEngine engine)
		{
			return new[]
			{
				new SubscriptionInfo(SubscriptionInfo.ParameterType.Table, 1100), // Outputs
			};
		}

		public override void ProcessParameterUpdate(IEngine engine, IConnectionHandlerEngine connectionEngine, ParameterUpdate update)
		{
			if (update.ParameterId != 1100)
			{
				// we are only interested in updates of the outputs table
				return;
			}

			var updatedConnections = new List<ConnectionUpdate>();

			var elementId = update.DmsElementId;

			if (update.UpdatedRows != null)
			{
				foreach (var row in update.UpdatedRows.Values)
				{
					var outputIdentifier = Convert.ToString(row[0]);
					var inputIdentifier = Convert.ToString(row[5]);

					var output = connectionEngine.Api.Endpoints.GetByRoleElementAndIdentifier(Role.Destination, elementId, outputIdentifier)
						?? throw new InvalidOperationException($"Destination endpoint '{outputIdentifier}' not found for element '{elementId}'.");
					
					if (String.IsNullOrWhiteSpace(inputIdentifier))
					{
						updatedConnections.Add(new ConnectionUpdate(output, isConnected: false));
						continue;
					}

					var input = connectionEngine.Api.Endpoints.GetByRoleElementAndIdentifier(Role.Source, elementId, inputIdentifier);

					if (input != null)
					{
						updatedConnections.Add(new ConnectionUpdate(input, output));
					}
					else
					{
						updatedConnections.Add(new ConnectionUpdate(output, isConnected: true));
					}
				}
			}

			if (update.DeletedRows != null)
			{
				foreach (var row in update.DeletedRows.Values)
				{
					var outputIdentifier = Convert.ToString(row[0]);

					var output = connectionEngine.Api.Endpoints.GetByRoleElementAndIdentifier(Role.Destination, elementId, outputIdentifier)
						?? throw new InvalidOperationException($"Destination endpoint '{outputIdentifier}' not found for element '{elementId}'.");
					
					updatedConnections.Add(new ConnectionUpdate(output, isConnected: false));
				}
			}

			if (updatedConnections.Count > 0)
			{
				connectionEngine.RegisterConnections(updatedConnections);
			}
		}

		public override void Connect(IEngine engine, IConnectionHandlerEngine connectionEngine, CreateConnectionsRequest createConnectionsRequest)
		{
			var groupedByDestinationElement = createConnectionsRequest.Connections.GroupBy(x => x.DestinationEndpoint.Element);

			foreach (var group in groupedByDestinationElement)
			{
				var elementId = group.Key;
				var element = engine.FindElementByKey(elementId);

				foreach (var connection in group)
				{
					var rowKey = connection.DestinationEndpoint.Identifier;
					var sourceIdentifier = connection.SourceEndpoint?.Identifier ?? String.Empty;

					element.SetParameterByPrimaryKey("Connected Input (Router Control Outputs)", rowKey, sourceIdentifier);
				}
			}
		}

		public override void Disconnect(IEngine engine, IConnectionHandlerEngine connectionEngine, DisconnectDestinationsRequest disconnectDestinationsRequest)
		{
			var groupedByDestinationElement = disconnectDestinationsRequest.Destinations.GroupBy(x => x.Element);

			foreach (var group in groupedByDestinationElement)
			{
				var elementId = group.Key;
				var element = engine.FindElementByKey(elementId);

				foreach (var destination in group)
				{
					var rowKey = destination.Identifier;

					element.SetParameterByPrimaryKey("Connected Input (Router Control Outputs)", rowKey, String.Empty);
				}
			}
		}
	}
}
