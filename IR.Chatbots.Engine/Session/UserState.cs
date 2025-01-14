﻿using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.ML;
using IR.Chatbots.Data.Models;
using IR.Chatbots.Data.Models.Neural;
using IR.Chatbots.Database.Extension;
using IR.Chatbots.Engine.Interfaces;
using IR.Chatbots.Engine.Request.Extensions;
using IR.Chatbots.ML;
using IR.Chatbots.ML.Interfaces;
using IR.Chatbots.ML.Models;
using IR.Chatbots.Session;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static IR.Chatbots.Database.Common.DbAlias;

namespace IR.Chatbots.Engine.Session
{
    /// <summary>
    /// Request current state.
    /// </summary>
    public enum ChatStateType
    {
        Start = 0,
        InvalidInput = 1,
        RecordFeedback = 2,
        ExpInput = 3,
        PickNode = 4,
        AdvanceChat = 5
    }

    /// <summary>
    /// User request and session state.
    /// </summary>
    public class RequestState
    {
        private NeuralLinkModel _rootLink;
        private NeuralLinkModel _currentLink;
        private ChatStateType _currentState = ChatStateType.Start;
        private string _botId;
        private string _userId;
        private IRequestPipeline _requestPipeline;
        private Stack<NeuralLinkModel> _linkHistory = new Stack<NeuralLinkModel>();

        public Stack<NeuralLinkModel> LinkHistory { get => _linkHistory; set => _linkHistory = value; }

        public ChatStateType CurrentState { get => _currentState; set => _currentState = value; }

        public string BotId => _botId;

        public string UserId => _userId;

        public NeuralLinkModel CurrentLink => _currentLink;

        public NeuralLinkModel RootLink => _rootLink;

        public IRequestPipeline RequestPipeline => _requestPipeline;

        public RequestState()
        {

        }

        public bool StepBack()
        {
            if (LinkHistory.Count < 2)  //Ignore root
                return false;

            NeuralLinkModel top;
            bool res = LinkHistory.TryPop(out top);
            res = LinkHistory.TryPop(out top);
            if (res)
            {
                StepForward(top);
            }
            return res;
        }

        public void StepForward(NeuralLinkModel link, bool recordHistory = true)
        {
            if (recordHistory)
                LinkHistory.Push(link);
            _currentLink = link;
            CurrentState = ChatStateType.Start;
        }

        public async Task Initialize(string userId, string botId, IRequestPipeline requestPipeline)
        {
            _botId = botId ?? throw new ArgumentNullException(nameof(botId));
            _userId = userId ?? throw new ArgumentNullException(nameof(userId));
            _requestPipeline = requestPipeline ?? throw new ArgumentNullException(nameof(requestPipeline));
            var chatProfile = await CurrentChatProfile();
            _currentLink = await DbLinkCollection.FindOneById(chatProfile?.Root ?? throw new InvalidOperationException($"Root does not exists for bot: {botId}"));
            _rootLink = CurrentLink;
            LinkHistory.Push(CurrentLink);
        }

        public async Task<int> HandleRequest(ITurnContext turnContext)
        {
            //First send back typing response and then process the request
            var typingReply = turnContext.Activity.CreateReply();
            typingReply.Type = ActivityTypes.Typing;
            await turnContext.SendActivityAsync(typingReply);

            var res = await RequestPipeline.Execute(turnContext, this);
            switch (res.Result)
            {
                case ResponseType.End:
                    {
                        await this.RemoveUserState();
                        var reply = turnContext.Activity.CreateReply(StringsProvider.TryGet(BotResourceKeyConstants.ThankYou));
                        reply.SuggestedActions = SuggestionExtension.GetFeedbackSuggestionActions(StringsProvider.TryGet(BotResourceKeyConstants.StartAgain));
                        await turnContext.SendActivityAsync(reply);
                    }
                    break;
                case ResponseType.Error:
                    break;
                case ResponseType.Continue:
                    await this.UpdateUserState();   //Update object in session storage
                    break;
                default:
                    break;
            }
            return res.Count;
        }
        public string Predict(string text) => PredictionEngineFactory.Predict(CurrentLink.Name, text);
    }
}
