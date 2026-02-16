using Sector0.Events;
using Prediction.data;
using Prediction.utils;
using UnityEngine;

namespace Prediction
{
    //TODO: document in readme
    public class ServerPredictedEntity : AbstractPredictedEntity
    {
        public static bool DEBUG = false;
        public static bool APPLY_FORCES_TO_EACH_CATCHUP_INPUT = false;
        public static bool USE_BUFFERING = true;
        public static bool BUFFER_ONCE = true;
        public static int BUFFER_FULL_THRESHOLD = 3; //Number of ticks to buffer before starting to send out the updates
        public static bool CATCHUP = true;
        public static bool INCREMENT_TICK_WHEN_NO_INPUT = false;
        public static bool SERVER_LOG_VELOCITIES = false;
        public static bool LOG_CLIENT_INUPTS = false;
        public static bool APPLY_OLD_INPUTS_IN_CURRENT_TICK = false;
        
        public GameObject gameObject;
        private PhysicsStateRecord serverStateBfr;
        private uint tickId;
        
        //TODO: package private
        public TickIndexedBuffer<PredictionInputRecord> inputQueue;
        
        //NOTE: Possible to buffer user inputs if needed to try and ensure a closer to client simulation on the server at the
        //cost of delaying the server behind the client by a small margin. The more you buffer, the more the server is delayed, the less reliable is the client image.
        
        private bool isBuffering = false;

        //NOTE: if the client updates buffer grows past a certain threshold
        //that means the server has fallen behind time wise. So we should snap ahead to the latest client state.
        public int catchupSections = 3;
        public int ticksPerCatchupSection = 0;
        
        //STATS
        public uint invalidInputs = 0;
        public uint ticksWithoutInput = 0;
        public uint lateTickCount = 0;
        public uint totalSnapAheadCounter = 0;
        public int inputJumps = 0;
        public uint catchupTicks = 0;
        public uint catchupBufferWipes = 0;
        public uint maxClientDelay = 0;
        public uint totalBufferingTicks = 0;
        public uint totalMissingInputTicks = 0;

        ~ServerPredictedEntity()
        {
            Debug.Log($"[ServerPredictedEntity] Destructor");
        }
        
        public ServerPredictedEntity(uint id, int bufferSize, Rigidbody rb, GameObject visuals, PredictableControllableComponent[] controllablePredictionContributors, PredictableComponent[] predictionContributors) : base(id, rb, visuals, controllablePredictionContributors, predictionContributors)
        {
            Debug.Log($"[ServerPredictedEntity] Constructor");
            gameObject = rb.gameObject;
            inputQueue = new TickIndexedBuffer<PredictionInputRecord>(bufferSize);
            inputQueue.emptyValue = null;

            ticksPerCatchupSection = Mathf.FloorToInt(bufferSize / catchupSections) + 1;
            serverStateBfr = PhysicsStateRecord.AllocWithComponentState(GetStateFloatCount(), GetStateBoolCount());
        }

        DesyncEvent devt = new DesyncEvent();
        private bool noInputAvailableForTick = false;
		bool tickUpdatedFromQueue = false;
		bool tickShouldUpdate = false;
		bool allInputsBehindTickId = false;
		uint inputsBehindBy = 0;

        void HandleTickInput()
        {
            //NOTE: this also loads TickId with the latest value
			allInputsBehindTickId = false;
			inputsBehindBy = 0;
			
			if (inputQueue.GetFill() > 0) {
				allInputsBehindTickId = true;
				uint maxDelay = inputQueue.GetRange();
            	if (maxDelay > maxClientDelay)
            	{
                	maxClientDelay = maxDelay;
            	}
					
				int inputsApplied = 0;
				tickUpdatedFromQueue = false;
				uint queueTickId = inputQueue.GetStartTick();
                PredictionInputRecord nextInput;
                do 
				{
					nextInput = inputQueue.Get(queueTickId);
					if (queueTickId >= tickId) {
                        int delta = (tickId > queueTickId ? -(int)(tickId - queueTickId) : (int)(queueTickId - tickId));
                        if (delta > 1)
                        {
                            inputJumps++;
                
                            devt.reason = DesyncReason.INPUT_JUMP;
                            devt.tickId = queueTickId;
                            potentialDesync.Dispatch(devt);
                        }
                        
						tickId = queueTickId;
						tickUpdatedFromQueue = true;
						allInputsBehindTickId = false;
						inputsBehindBy = 0;
                        
                        LoadValidateApplyInput(tickId, nextInput);
						break;
					} 
                    else if (APPLY_OLD_INPUTS_IN_CURRENT_TICK)
                    {
                        LoadValidateApplyInput(tickId, nextInput);
                        
                        devt.reason = DesyncReason.LATE_TICK;
                        devt.tickId = queueTickId;
                        potentialDesync.Dispatch(devt);
                    }
                    
					inputQueue.Remove(queueTickId);
					//TODO: do something about this drifting behind
					inputsBehindBy = tickId - queueTickId;
					queueTickId++;
					if (nextInput != null) {
						inputsApplied++;
					}
				} while(nextInput != null);

				if (inputsApplied > 1)
            	{
                	devt.reason = DesyncReason.MULTIPLE_INPUTS_PER_FRAME;
                	devt.tickId = tickId;
                	potentialDesync.Dispatch(devt);
            	}
			} 
			else 
			{
				ticksWithoutInput++;
                
                devt.reason = DesyncReason.NO_INPUT_FOR_SERVER_TICK;
                devt.tickId = tickId;
                potentialDesync.Dispatch(devt);
			}
		
			ApplyForces();
        }

        private uint lastInputLoadedTick = 0;
		void LoadValidateApplyInput(uint qTickId, PredictionInputRecord nextInput) {
			if (nextInput == null)
	            return;

            if (LOG_CLIENT_INUPTS)
            {
                Debug.Log($"[SV][SIMULATION][INPUT] i:{id} t:{qTickId} input:{nextInput}");
            }

            int delta = (int) (qTickId - lastInputLoadedTick);
            if (ValidateState(TickDeltaToTimeDelta(delta), nextInput))
            {
                lastInputLoadedTick = qTickId;
                LoadInput(nextInput);
            }
            else
            {
                invalidInputs++;
                
                devt.reason = DesyncReason.INVALID_INPUT;
                devt.tickId = qTickId;
                potentialDesync.Dispatch(devt);
            }
		}
		
		public static bool LOG_INPUT_QUEUE_SIZE = true;
        public uint ServerSimulationTick()
        {
			if (LOG_INPUT_QUEUE_SIZE)
				Debug.Log($"[ServerPredictedEntity][ServerSimulationTick] i:{id} t:{tickId} inputBufferSize:{inputQueue.GetFill()}");
            
			if (CanUseBuffer())
            {
				//TODO: better mechanism for controlling when to update tickID?
				tickShouldUpdate = false;
				//NOTE: this runs even if the buffer is empty, and has the purpose of advancing the tick_id and applying forces.
				HandleTickInput();
                if (CATCHUP)
                {
                    int catchup = GetCatchupInputsCount();
                    devt.reason = DesyncReason.CATCHUP;
                    devt.tickId = tickId;
                    potentialDesync.Dispatch(devt);
                    
                    while (catchup > 0)
                    {
                        catchup--;
                        catchupTicks++;
                        HandleTickInput();
                    }   
                }
            }
            else
            {
                //Tick only goes up on its own if no user input, if there is user input we go back to using those,
                //if all of those are in the past, that means we're still somehow ahead of the client so don't continue increasing the tick_id
                //once enough inputs from the client arrive, the tick_id will continue to increase. However this scenario should be logged, the client falling behind is a problem.
				tickShouldUpdate = !isBuffering && INCREMENT_TICK_WHEN_NO_INPUT;
                
                if (inputQueue.GetFill() > 0)
                {
                    totalBufferingTicks++;
                    
                    devt.reason = DesyncReason.INPUT_BUFFERED;
                    devt.tickId = tickId;
                    potentialDesync.Dispatch(devt);
                }
                else
                {
                    totalMissingInputTicks++;
                    
                    devt.reason = DesyncReason.NO_INPUT_FOR_SERVER_TICK;
                    devt.tickId = tickId;
                    potentialDesync.Dispatch(devt);
                }
                ApplyForces();
            }
            return GetTickId();
        }

        int GetCatchupInputsCount()
        {
            if (!CATCHUP)
                return 0;
            return Mathf.FloorToInt(inputQueue.GetFill() / ticksPerCatchupSection);
        }
        
        public PhysicsStateRecord SamplePhysicsState()
        {
            preSampleState.Dispatch(true);
            PopulatePhysicsStateRecord(GetTickId(), serverStateBfr);
            serverStateBfr.input = inputQueue.Remove(GetTickId());
            SampleComponentState(serverStateBfr);
            stateSampled.Dispatch(true);
            
			if (DEBUG)
                Debug.Log($"[ServerPredictedEntity][SamplePhysicsState]({id}) input:{serverStateBfr}");
            
            if (SERVER_LOG_VELOCITIES)
            {
                //TODO: this can be done via events in the host application...
                Debug.Log($"[SV][SIMULATION][DATA] i:{id} t:{tickId} p:{rigidbody.position.ToString("F10")} r:{rigidbody.rotation.ToString("F10")} v:{rigidbody.linearVelocity.ToString("F10")} a:{rigidbody.angularVelocity.ToString("F10")} input:{serverStateBfr.input}");
            }

			if (tickShouldUpdate) {
				tickId++;
			}
            return serverStateBfr;
        }

        public float TickDeltaToTimeDelta(int delta)
        {
            //TODO: have fixedDeltaTime be configurable, pass that in on instantiation
            return delta * Time.fixedDeltaTime;
        }

        public uint clUpdateCount = 0;
        public uint clAddedUpdateCount = 0;
        private ClientInput cevt;
        public void BufferClientTick(uint clientTickId, PredictionInputRecord inputRecord)
        {
            if (DEBUG)
                Debug.Log($"[ServerPredictedEntity][BufferClientTick]({gameObject.GetInstanceID()}) clientTickId:{clientTickId} tickId:{tickId} input:{inputRecord}");
            
            if (inputQueue.GetFill() == 0)
            {
                firstTickArrived.Dispatch(true);
            }

            clUpdateCount++;
            if (inputQueue.GetFill() == inputQueue.GetCapacity())
            {
                devt.reason = DesyncReason.TICK_OVERFLOW;
                devt.tickId = tickId;
                potentialDesync.Dispatch(devt);
            }
            clAddedUpdateCount++;
            inputQueue.Add(clientTickId, inputRecord);
            
            if (clientTickId < tickId)
            {
                lateTickCount++;
                devt.reason = DesyncReason.LATE_TICK;
                devt.tickId = tickId;
                potentialDesync.Dispatch(devt);
            }

            if (isBuffering)
            {
                isBuffering = inputQueue.GetFill() < BUFFER_FULL_THRESHOLD;
	
            }

            if (LOG_CLIENT_INUPTS)
            {
                cevt.tickId = tickId;
                cevt.input = inputRecord;
                inputReceived.Dispatch(cevt);   
            }
        }
        
        public void ResetClientState()
        {
            //NOTE: use this when changing the controller of the plane.
            tickId = 0;
            inputQueue.Clear();
            isBuffering = USE_BUFFERING;
        }
        
        public uint GetTickId()
        {
            return tickId;
        }

        //TODO: unit test the buffering
        bool CanUseBuffer()
        {
            return !isBuffering;
        }

        public int BufferFill()
        {
            return inputQueue.GetFill();
        }
        
        public uint BufferSize()
        {
            return inputQueue.GetRange();
        }
        
        //NOTE: call this when you change the owner of the object
        public void Reset()
        {
            inputQueue.Clear();
			isBuffering = USE_BUFFERING;
			tickId = 0;
            
            invalidInputs = 0;
            ticksWithoutInput = 0;
            lateTickCount = 0;
        }
        
        //TODO: decide if to keep?
        void SnapToLatest()
        {
            if (DEBUG)
                Debug.Log($"[ServerPredictedEntity][SnapToLatest]({gameObject.GetInstanceID()})");
            totalSnapAheadCounter++;

            uint tick = inputQueue.GetEndTick();
            PredictionInputRecord pir = inputQueue.GetEnd();
            inputQueue.Clear();
            inputQueue.Add(tick, pir);
        }

        public enum DesyncReason
        {
            NO_INPUT_FOR_SERVER_TICK = 0,
            INPUT_BUFFERED = 1,
            INPUT_JUMP = 2,
            MULTIPLE_INPUTS_PER_FRAME = 3,
            INVALID_INPUT = 4,
            LATE_TICK = 5,
            TICK_OVERFLOW = 6,
			CATCHUP = 7,
        }
        public struct DesyncEvent
        {
            public uint tickId;
            public DesyncReason reason;
        }

        public struct ClientInput
        {
            public uint tickId;
            public PredictionInputRecord input;
        }
        
        public SafeEventDispatcher<bool> preSampleState = new();
        public SafeEventDispatcher<bool> firstTickArrived = new();
        public SafeEventDispatcher<DesyncEvent> potentialDesync = new();
        public SafeEventDispatcher<bool> stateSampled = new();
        public SafeEventDispatcher<ClientInput> inputReceived = new();
    }
}