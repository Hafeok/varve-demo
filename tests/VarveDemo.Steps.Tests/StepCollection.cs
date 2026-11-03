// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Xunit;

namespace VarveDemo.Steps.Tests;

/// <summary>The step checks share one compose stack, so they never run at once.</summary>
[CollectionDefinition(nameof(StepChecks), DisableParallelization = true)]
public sealed class StepChecksDefinition
{
}
