using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using Azure.AI.Projects;
using System.Text;
namespace RxMind.Agents;

#pragma warning disable OPENAI001
public class RxMindWorkflow
{
    private readonly Workflow _workflow;

    public RxMindWorkflow()
    {
        // Reference https://learn.microsoft.com/en-us/agent-framework/workflows/orchestrations/sequential?pivots=programming-language-csharp
        // One shared IChatClient backed by Azure OpenAI
        var endpoint = Config.OpenAIEndpoint;
        var deploymentName = Config.ModelDeployment;
        var credential = new CachedTokenCredential(new DefaultAzureCredential());
        var chatClient = new AIProjectClient(new Uri(endpoint), credential)
            .GetProjectOpenAIClient()
            .GetProjectResponsesClient()
            .AsIChatClient(deploymentName)
            .AsBuilder()
            .Use(inner => new ContentSafetyMiddleware(inner))
            .Build();

        var kb = new KnowledgeBaseService();
        var searchTool = AIFunctionFactory.Create(kb.SearchAsync, "SearchKnowledgeBase", "Search the RxMind formulary and policy knowledge base");
        
        // Create agents — each wrapped with OpenTelemetryAgent for tracing in App Insights
        var intakeAgent = new OpenTelemetryAgent(
            new ChatClientAgent(
                chatClient,
                """
                You are an intake specialist for RxMind specialty pharmacy.
                A pharmacist or admin has entered prescription details received from a patient or prescriber.
                Extract: Patient name, Medication name, Dosage, Insurance provider, Prescriber name.
                If any field is missing, note it as "Not provided".
                Return a clearly labeled INTAKE REPORT with each field on its own line.
                """,
                "IntakeAgent"),
            sourceName: "RxMind");

        var clinicalAgent = new OpenTelemetryAgent(
            new ChatClientAgent(
                chatClient,
                """
                You are a clinical pharmacist for RxMind specialty pharmacy.
                You will receive an INTAKE REPORT entered by pharmacy staff. Based on the medication mentioned:
                - Check for common drug interactions
                - Confirm the dosage is within safe range
                - Note any clinical warnings or special handling requirements
                Use the SearchKnowledgeBase tool to look up formulary and clinical policy information.
                Write your findings for pharmacy staff review — be precise and clinical.
                Add a CLINICAL REPORT section to the input you received and return everything.
                """,
                "ClinicalAgent",
                null,
                [searchTool]),
            sourceName: "RxMind");

        var operationsAgent = new OpenTelemetryAgent(
            new ChatClientAgent(
                chatClient,
                """
                You are an operations specialist for RxMind specialty pharmacy.
                You will receive an INTAKE REPORT and CLINICAL REPORT compiled by pharmacy staff. Based on the medication and insurance:
                - Identify if Prior Authorization (PA) is required and outline the steps
                - Estimate delivery timeline (standard 3-5 days, specialty 7-10 days)
                - Note any financial assistance programs available for this medication
                Use the SearchKnowledgeBase tool to look up PA requirements and financial assistance programs.
                Write your findings for pharmacy staff to act on.
                Add an OPERATIONS REPORT section to the input you received and return everything.
                """,
                "OperationsAgent",
                null,
                [searchTool]),
            sourceName: "RxMind");

        var orchestratorAgent = new OpenTelemetryAgent(
            new ChatClientAgent(
                chatClient,
                """
                You are the final orchestrator for RxMind specialty pharmacy.
                You will receive an INTAKE REPORT, CLINICAL REPORT, and OPERATIONS REPORT compiled by pharmacy staff.
                Compile a clear FINAL SUMMARY for the pharmacist to review and act on, covering:
                - Patient and prescription overview
                - Key clinical findings and warnings
                - Next steps: PA requirements, delivery timeline, financial assistance
                Be concise and professional — this is an internal staff-facing report, not a patient communication.
                """,
                "OrchestratorAgent"),
            sourceName: "RxMind");

        // Sequential workflow: Intake -> Clinical -> Operations -> Orchestrator
        // each agent gets the entire conversation history
        _workflow = AgentWorkflowBuilder.BuildSequential(
            [intakeAgent, clinicalAgent, operationsAgent, orchestratorAgent]);
    }

    public async Task<string> ProcessAsync(string patientInput)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, patientInput)
        };

        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(_workflow, messages); // get events back one by one as each agent finishes, instead of waiting for all 4 agents to complete before getting anything. || the object that represents the running workflow.
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true)); // TurnToken is the "go" signal + broadcast events

        string? lastExecutorId = null;
        var lastAgentText = new StringBuilder();

        // Reference https://learn.microsoft.com/en-us/agent-framework/workflows/orchestrations/sequential?pivots=programming-language-csharp#set-up-the-sequential-orchestration
        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is AgentResponseUpdateEvent e) //Workflow outputs data. Reference https://learn.microsoft.com/en-us/agent-framework/workflows/events?pivots=programming-language-csharp
            {
                if (e.ExecutorId != lastExecutorId)
                {
                    lastExecutorId = e.ExecutorId;
                    lastAgentText.Clear();
                }

                lastAgentText.Append(e.Update.Text);
            }
            else if (evt is WorkflowOutputEvent)
            {
                break;
            }
        }
        return lastAgentText.ToString();
    }
}
