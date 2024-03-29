using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using NBitcoin.RPC;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Backend.Controllers;

/// <summary>
/// To make batched requests.
/// </summary>
[Produces("application/json")]
[Route("api/v" + Constants.BackendMajorVersion + "/btc/[controller]")]
public class BatchController : ControllerBase
{
	public BatchController(BlockchainController blockchainController, HomeController homeController, IndexBuilderService indexBuilderService)
	{
		BlockchainController = blockchainController;
		HomeController = homeController;
		IndexBuilderService = indexBuilderService;
	}

	public BlockchainController BlockchainController { get; }
	public HomeController HomeController { get; }
	public IndexBuilderService IndexBuilderService { get; }

	[HttpGet("synchronize")]
	public async Task<IActionResult> GetSynchronizeAsync([FromQuery, Required] string bestKnownBlockHash, [FromQuery, Required] int maxNumberOfFilters, [FromQuery] string? estimateSmartFeeMode = nameof(EstimateSmartFeeMode.Conservative))
	{
		bool estimateSmartFee = !string.IsNullOrWhiteSpace(estimateSmartFeeMode);
		EstimateSmartFeeMode mode = EstimateSmartFeeMode.Conservative;
		if (estimateSmartFee)
		{
			if (!Enum.TryParse(estimateSmartFeeMode, ignoreCase: true, out mode))
			{
				return BadRequest("Invalid estimation mode is provided, possible values: ECONOMICAL/CONSERVATIVE.");
			}
		}

		if (!uint256.TryParse(bestKnownBlockHash, out var knownHash))
		{
			return BadRequest($"Invalid {nameof(bestKnownBlockHash)}.");
		}

		(Height bestHeight, IEnumerable<FilterModel> filters) = IndexBuilderService.GetFilterLinesExcluding(knownHash, maxNumberOfFilters, out bool found);

		var response = new SynchronizeResponse { Filters = Enumerable.Empty<FilterModel>(), BestHeight = bestHeight };

		if (!found)
		{
			response.FiltersResponseState = FiltersResponseState.BestKnownHashNotFound;
		}
		else if (!filters.Any())
		{
			response.FiltersResponseState = FiltersResponseState.NoNewFilter;
		}
		else
		{
			response.FiltersResponseState = FiltersResponseState.NewFilters;
			response.Filters = filters;
		}

		if (estimateSmartFee)
		{
			response.AllFeeEstimate = await BlockchainController.GetAllFeeEstimateAsync(mode);
		}

		return Ok(response);
	}
}
