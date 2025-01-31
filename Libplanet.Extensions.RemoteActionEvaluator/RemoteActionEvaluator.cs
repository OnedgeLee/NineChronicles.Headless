using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Net.Http.Json;
using Bencodex.Types;
using Lib9c.StateService;
using Lib9c.StateService.Shared;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet.Consensus;
using Libplanet.Extensions.ActionEvaluatorCommonComponents;
using Libplanet.State;

namespace Libplanet.Extensions.RemoteActionEvaluator;

public class RemoteActionEvaluator : IActionEvaluator
{
    private readonly Uri _endpoint;
    private readonly IBlockChainStates _blockChainStates;

    public RemoteActionEvaluator(Uri endpoint, IBlockChainStates blockChainStates)
    {
        _endpoint = endpoint;
        _blockChainStates = blockChainStates;
    }

    public IActionLoader ActionLoader => throw new NotSupportedException();

    public IReadOnlyList<IActionEvaluation> Evaluate(IPreEvaluationBlock block)
    {
        using var httpClient = new HttpClient();
        var response = httpClient.PostAsJsonAsync(_endpoint, new RemoteEvaluationRequest
        {
            PreEvaluationBlock = PreEvaluationBlockMarshaller.Serialize(block),
        }).Result;
        var evaluationResponse = response.Content.ReadFromJsonAsync<RemoteEvaluationResponse>().Result;

        var actionEvaluations = evaluationResponse.Evaluations.Select(ActionEvaluationMarshaller.Deserialize)
            .ToImmutableList();

        for (var i = 0; i < actionEvaluations.Count; ++i)
        {
            if (i > 0)
            {
                actionEvaluations[i].InputContext.PreviousStates.StateGetter =
                    actionEvaluations[i - 1].OutputStates.GetStates;
                actionEvaluations[i].InputContext.PreviousStates.BalanceGetter =
                    actionEvaluations[i - 1].OutputStates.GetBalance;
                actionEvaluations[i].InputContext.PreviousStates.TotalSupplyGetter =
                    actionEvaluations[i - 1].OutputStates.GetTotalSupply;
                actionEvaluations[i].InputContext.PreviousStates.ValidatorSetGetter =
                    actionEvaluations[i - 1].OutputStates.GetValidatorSet;
            }
            else
            {
                (
                    actionEvaluations[i].InputContext.PreviousStates.StateGetter,
                    actionEvaluations[i].InputContext.PreviousStates.BalanceGetter,
                    actionEvaluations[i].InputContext.PreviousStates.TotalSupplyGetter,
                    actionEvaluations[i].InputContext.PreviousStates.ValidatorSetGetter
                ) = InitializeAccountGettersPair(block);
            }

            actionEvaluations[i].OutputStates.StateGetter =
                actionEvaluations[i].InputContext.PreviousStates.GetStates;
            actionEvaluations[i].OutputStates.BalanceGetter =
                actionEvaluations[i].InputContext.PreviousStates.GetBalance;
            actionEvaluations[i].OutputStates.TotalSupplyGetter =
                actionEvaluations[i].InputContext.PreviousStates.GetTotalSupply;
            actionEvaluations[i].OutputStates.ValidatorSetGetter =
                actionEvaluations[i].InputContext.PreviousStates.GetValidatorSet;
        }

        return actionEvaluations;
    }

    private (AccountStateGetter, AccountBalanceGetter, TotalSupplyGetter, ValidatorSetGetter)
        InitializeAccountGettersPair(
            IPreEvaluationBlockHeader blockHeader)
    {
        AccountStateGetter accountStateGetter;
        AccountBalanceGetter accountBalanceGetter;
        TotalSupplyGetter totalSupplyGetter;
        ValidatorSetGetter validatorSetGetter;

        if (blockHeader.PreviousHash is { } previousHash)
        {
            accountStateGetter = addresses => _blockChainStates.GetStates(
                addresses,
                previousHash
            );
            accountBalanceGetter = (address, currency) => _blockChainStates.GetBalance(
                address,
                currency,
                previousHash
            );
            totalSupplyGetter = currency => _blockChainStates.GetTotalSupply(
                currency,
                previousHash
            );
            validatorSetGetter = () => _blockChainStates.GetValidatorSet(previousHash);
        }
        else
        {
            accountStateGetter = NullAccountStateGetter;
            accountBalanceGetter = NullAccountBalanceGetter;
            totalSupplyGetter = NullTotalSupplyGetter;
            validatorSetGetter = NullValidatorSetGetter;
        }

        return (accountStateGetter, accountBalanceGetter, totalSupplyGetter,
            validatorSetGetter);
    }

    [Pure]
    private static IReadOnlyList<IValue?> NullAccountStateGetter(
        IReadOnlyList<Address> addresses
    ) =>
        new IValue?[addresses.Count];

    [Pure]
    private static FungibleAssetValue NullAccountBalanceGetter(
        Address address,
        Currency currency
    ) =>
        currency * 0;

    [Pure]
    private static FungibleAssetValue NullTotalSupplyGetter(Currency currency)
    {
        if (!currency.TotalSupplyTrackable)
        {
            throw WithDefaultMessage(currency);
        }

        return currency * 0;
    }

    [Pure]
    private static ValidatorSet NullValidatorSetGetter()
    {
        return new ValidatorSet();
    }

    private static TotalSupplyNotTrackableException WithDefaultMessage(Currency currency)
    {
        var msg =
            $"The total supply value of the currency {currency} is not trackable because it"
            + " is a legacy untracked currency which might have been established before"
            + " the introduction of total supply tracking support.";
        return new TotalSupplyNotTrackableException(msg, currency);
    }
}
