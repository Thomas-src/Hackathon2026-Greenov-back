using Backend.Domains;

namespace Backend.Services;

public class ActionEngine
{
    public ActionResult Evaluate(GameAction action,
        List<string> playerHistory, // actions déjà faites par ce joueur sur cet event
        GameEvent gameEvent)
    {
        var prereqsMet = action.RequiredBefore
            .All(required => playerHistory.Contains(required));

        if (!prereqsMet)
        {
            // Pénalité : B fait avant A
            return new ActionResult(
                action.Id,
                PrerequisitesMet: false,
                ScoreApplied: action.ScoreDelta < 0 ? action.ScoreDelta * 2 : -10,
                CarbonApplied: Math.Abs(action.CarbonDelta) + 10,
                IsGoodMove:  false
            );
        }

        return new ActionResult(
            action.Id,
            PrerequisitesMet: true,
            ScoreApplied: action.ScoreDelta,
            CarbonApplied: action.CarbonDelta,
            IsGoodMove:  true
        );
    }
}