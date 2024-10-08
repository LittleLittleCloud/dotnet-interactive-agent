﻿using AutoGen.Core;
using AutoGen.DotnetInteractive.Extension;
using AutoGen.OpenAI;
using Azure.AI.OpenAI;
using Microsoft.DotNet.Interactive;
using Microsoft.SemanticKernel.Agents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotnet_interactive_agent.Agent;

internal class Runner : IAgent, IDisposable
{
    private readonly Kernel _kernel;
    private Runner(Kernel kernel, string name = "runner")
    {
        _kernel = kernel;
        Name = name;
    }

    public static IAgent CreateFromOpenAI(Kernel kernel, string name = "runner")
    {
        return new Runner(kernel, name);
    }

    public string Name { get; }

    public void Dispose()
    {
        _kernel.Dispose();
    }

    public async Task<IMessage> GenerateReplyAsync(IEnumerable<IMessage> messages, GenerateReplyOptions? options = null, CancellationToken cancellationToken = default)
    {
        var lastMessage = messages.Last() ?? throw new InvalidOperationException("No message to reply to");
        var lastState = messages.LastOrDefault()?.GetState();

        if (lastState is State runCode
            && runCode.CurrentStep == Step.RunCode
            && runCode.Task is string
            && runCode.Code is string)
        {
            var sb = new StringBuilder();
            var codeMessage = new TextMessage(Role.Assistant, runCode.Code);
            // process python block
            foreach (var pythonCode in codeMessage.ExtractCodeBlocks("```python", "```"))
            {
                var codeResult = await this._kernel.RunSubmitCodeCommandAsync(pythonCode, "python", cancellationToken);

                codeResult = $"""
                [Python Code Block]
                ```python
                {pythonCode}
                ```

                [Execute Result]
                {codeResult}
                """;

                sb.AppendLine(codeResult);
            }

            // process powershell block
            foreach (var pwshCode in codeMessage.ExtractCodeBlocks("```pwsh", "```"))
            {
                var codeResult = await this._kernel.RunSubmitCodeCommandAsync(pwshCode, "pwsh", cancellationToken);

                codeResult = $"""
                [Powershell Code Block]
                ```pwsh
                {pwshCode}
                ```

                [Execute Result]
                {codeResult}
                """;

                sb.AppendLine(codeResult);
            }


            // process csharp block
            foreach (var csharpCode in codeMessage.ExtractCodeBlocks("```csharp", "```"))
            {
                var codeResult = await this._kernel.RunSubmitCodeCommandAsync(csharpCode, "csharp", cancellationToken);

                codeResult = $"""
                [CSharp Code Block]
                ```csharp
                {csharpCode}
                ```

                [Execute Result]
                {codeResult}
                """;

                sb.AppendLine(codeResult);
            }

            Console.WriteLine(sb.ToString());
            var codeExecutionResult = sb.ToString();

            return new TextMessage(Role.Assistant, codeExecutionResult, from: this.Name);
        }

        throw new InvalidOperationException("Unexpected message type");
    }
}
