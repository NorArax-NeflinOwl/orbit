<#
.SYNOPSIS
Mails the person holding the card what to run, when the budget fires and when the month ends.

.DESCRIPTION
An Azure budget's own email says a number and nothing else. This runbook, called by the budget's
action group, replaces that with the mail that was asked for: at the 10 EUR warning, what to look at;
at the 20 EUR ceiling, the exact commands that stop the spending - and on the last day of the month,
if anything is still stopped, the commands that bring it back before the counter resets.

It never stops or starts anything itself. The card's owner stays the one who runs the commands, which
is the decision info/azure-setup.md ("Cost limits") records; the runbook's identity holds Reader on the
resource group and nothing more, so it could not do otherwise.

Two ways in, one runbook:
  - the action group's webhook, carrying Azure's budget notification JSON in -WebhookData;
  - a daily schedule, with no parameters - the runbook checks whether tomorrow is the 1st and exits
    at once on every other day. Daily-and-decide rather than a "last day of month" schedule, so the
    behaviour does not hang on how Automation interprets month lengths.

Automation assets it reads (variables and one credential, set up per info/azure-setup.md):
  SmtpHost, SmtpPort, SmtpFromAddress, MailToAddress    - Automation variables
  SmtpCredential                                        - Automation credential (user name + password)
  BudgetAmount                                          - Automation variable, the ceiling, e.g. 20
  ResourceGroup                                         - Automation variable, "Orbit"

.NOTES
Runtime: PowerShell 7.2 in Azure Automation. Only Az.Accounts is needed - every Azure read goes
through Invoke-AzRestMethod, so no extra modules have to be imported into the account.
#>
param(
    [object] $WebhookData
)

$ErrorActionPreference = 'Stop'

function Read-Setting([string] $name) {
    $value = Get-AutomationVariable -Name $name
    if ([string]::IsNullOrWhiteSpace($value)) { throw "Automation variable '$name' is not set." }
    return $value
}

function Send-InstructionMail([string] $subject, [string] $body) {
    $credential = Get-AutomationPSCredential -Name 'SmtpCredential'
    Send-MailMessage `
        -SmtpServer (Read-Setting 'SmtpHost') `
        -Port ([int](Read-Setting 'SmtpPort')) `
        -UseSsl `
        -Credential $credential `
        -From (Read-Setting 'SmtpFromAddress') `
        -To (Read-Setting 'MailToAddress') `
        -Subject $subject `
        -Body $body
    Write-Output "Sent: $subject"
}

# One sign-in for every read below. Reader on the resource group is all the identity has.
function Connect-WithManagedIdentity {
    Connect-AzAccount -Identity | Out-Null
}

function Get-ResourceJson([string] $resourcePath, [string] $apiVersion) {
    $response = Invoke-AzRestMethod -Method GET -Path "$resourcePath`?api-version=$apiVersion"
    if ($response.StatusCode -ne 200) { throw "GET $resourcePath answered $($response.StatusCode)." }
    return $response.Content | ConvertFrom-Json
}

# The server carries a random suffix, so it is looked up rather than named - the same reason
# scripts/stop-azure-compute.sh does.
function Get-DeploymentState {
    $subscriptionId = (Get-AzContext).Subscription.Id
    $resourceGroup = Read-Setting 'ResourceGroup'
    $groupPath = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup"

    $servers = (Get-ResourceJson "$groupPath/providers/Microsoft.DBforPostgreSQL/flexibleServers" '2022-12-01').value
    $server = $servers | Select-Object -First 1

    $applications = foreach ($name in 'orbit-api', 'orbit-web') {
        $application = Get-ResourceJson "$groupPath/providers/Microsoft.App/containerApps/$name" '2024-03-01'
        [pscustomobject]@{
            Name = $name
            IngressOpen = $null -ne $application.properties.configuration.ingress
        }
    }

    return [pscustomobject]@{
        ResourceGroup = $resourceGroup
        PostgresName = $server.name
        PostgresState = $server.properties.state
        Applications = $applications
    }
}

function Test-AnythingStopped($state) {
    if ($state.PostgresState -eq 'Stopped') { return $true }
    return ($state.Applications | Where-Object { -not $_.IngressOpen }).Count -gt 0
}

# The commands are spelled out in full rather than only as the script's name, because the person
# reading this mail may be on a phone with no clone of the repository in reach.
function Get-StopCommands($state) {
    @"
Everything below is one command each, from any shell signed in with 'az login'. Nothing is deleted.

  az postgres flexible-server stop -g $($state.ResourceGroup) -n $($state.PostgresName)
  az containerapp ingress disable -n orbit-api -g $($state.ResourceGroup)
  az containerapp update -n orbit-api -g $($state.ResourceGroup) --min-replicas 0
  az containerapp ingress disable -n orbit-web -g $($state.ResourceGroup)
  az containerapp update -n orbit-web -g $($state.ResourceGroup) --min-replicas 0

With the repository at hand, the same five are:

  scripts/stop-azure-compute.sh

Orbit is then down for everyone until the resume commands are run - you will get them by mail on the
last day of the month, if anything is still stopped. Azure restarts a stopped PostgreSQL server on its
own after seven days; if the month has time left at that point, stop it again.
"@
}

function Get-ResumeCommands($state) {
    @"
The budget resets tomorrow, and something is still stopped:

  PostgreSQL $($state.PostgresName): $($state.PostgresState)
$(($state.Applications | ForEach-Object { "  $($_.Name): ingress " + $(if ($_.IngressOpen) { 'open' } else { 'closed' }) }) -join "`n")

To bring Orbit back, in this order - the database first, because orbit-api applies migrations at
startup and never becomes healthy without it:

  az postgres flexible-server start -g $($state.ResourceGroup) -n $($state.PostgresName)
  az containerapp update -n orbit-api -g $($state.ResourceGroup) --min-replicas 1
  az containerapp ingress enable -n orbit-api -g $($state.ResourceGroup) --type external --target-port 8080 --transport auto
  az containerapp update -n orbit-web -g $($state.ResourceGroup) --min-replicas 0
  az containerapp ingress enable -n orbit-web -g $($state.ResourceGroup) --type external --target-port 80 --transport auto

With the repository at hand:

  scripts/stop-azure-compute.sh --resume

Then check the newest revision of each app reaches Healthy:

  az containerapp revision list -n orbit-api -g $($state.ResourceGroup) -o table
"@
}

function Send-BudgetMail($notification) {
    $spent = [decimal] $notification.SpendingAmount
    $threshold = [decimal] $notification.NotificationThresholdAmount
    $budget = [decimal] $notification.Budget
    $unit = $notification.Unit

    Connect-WithManagedIdentity
    $state = Get-DeploymentState

    if ($threshold -lt $budget) {
        $subject = "Orbit on Azure: $spent $unit of $budget $unit spent this month - halfway warning"
        $body = @"
The subscription has spent $spent $unit this month, past the $threshold $unit warning line.
Nothing has to be turned off. Halfway through the month this is normal; halfway through the ceiling
on the 5th is not. Where it went:

  scripts/stop-azure-compute.sh --status
  az consumption usage list --start-date <first of month> --end-date <today> --query "[].[instanceName, pretaxCost]" -o tsv

The usual reasons, in the order worth checking: autoscaling left on after a test, orbit-web never
scaling to zero because somebody has Orbit open, a deploy loop that pushed far more images than usual.

The next mail, at $budget $unit, carries the commands that stop the spending.
"@
        Send-InstructionMail $subject $body
        return
    }

    $subject = "Orbit on Azure: $spent $unit of $budget $unit - the ceiling. Commands to stop the spending inside"
    $body = "The subscription has reached its monthly ceiling: $spent $unit against $budget $unit.`n`n" + (Get-StopCommands $state)
    Send-InstructionMail $subject $body
}

function Send-MonthEndMailIfStopped {
    $tomorrow = (Get-Date).ToUniversalTime().AddDays(1)
    if ($tomorrow.Day -ne 1) {
        Write-Output "Not the last day of the month (UTC). Nothing to do."
        return
    }

    Connect-WithManagedIdentity
    $state = Get-DeploymentState
    if (-not (Test-AnythingStopped $state)) {
        Write-Output "Last day of the month and nothing is stopped. Nothing to do."
        return
    }

    Send-InstructionMail 'Orbit on Azure: the budget resets tomorrow - commands to start it again inside' (Get-ResumeCommands $state)
}

if ($null -ne $WebhookData) {
    # Azure posts its budget notification as JSON; when tested from the portal the same arrives as a
    # string in RequestBody, which is why both shapes are accepted.
    $requestBody = if ($WebhookData -is [string]) { ($WebhookData | ConvertFrom-Json).RequestBody } else { $WebhookData.RequestBody }
    $payload = $requestBody | ConvertFrom-Json
    if ($payload.schemaId -ne 'AIP Budget Notification') {
        throw "Unexpected webhook payload (schemaId '$($payload.schemaId)'). Only budget notifications are handled."
    }
    Send-BudgetMail $payload.data
    return
}

Send-MonthEndMailIfStopped
