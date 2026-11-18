# Mizan demo data: wipes the tickets and rebuilds a set that covers every status.
#
# Why through the API and not SQL inserts: every ticket goes through the real workflow
# rules (TicketWorkflow) as the real role, so the history, snapshots and responses are
# exactly what the app would produce. If a rule rejects a step, this script fails loudly.
#
# Needs: the API running (Development, so the seeded users exist).
# Run from F:\Ticketing:   powershell -ExecutionPolicy Bypass -File scripts\demo-data.ps1
# Deletes ALL tickets in the local MizanDb. Users and the team are kept.

param([string]$Api = 'http://localhost:5199')

$ErrorActionPreference = 'Stop'

# Only ever the local dev database, never a connection string from config
$connectionString = 'Server=(localdb)\MSSQLLocalDB;Database=MizanDb;Trusted_Connection=True;TrustServerCertificate=True'
$password = 'Mizan@123'   # the demo password from DbSeeder

# --- 1. Clear the ticket tables (children first, because every FK is Restrict) ---
Write-Host 'Clearing tickets from MizanDb (LocalDB)...'
$sql = @'
DELETE FROM AuditLogs;
DELETE FROM Comments;
DELETE FROM TicketResponses;
DELETE FROM CodeSnapshots;
DELETE FROM TicketAssignments;
DELETE FROM Tickets;
'@
$conn = New-Object System.Data.SqlClient.SqlConnection $connectionString
$conn.Open()
try {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    [void]$cmd.ExecuteNonQuery()
} finally {
    $conn.Close()
}

# --- 2. Helpers ---
function Get-Token([string]$email) {
    $body = @{ email = $email; password = $password } | ConvertTo-Json
    (Invoke-RestMethod "$Api/api/auth/login" -Method Post -ContentType 'application/json' -Body $body).accessToken
}

function Invoke-Api([string]$token, [string]$method, [string]$path, $body = $null) {
    $request = @{
        Uri         = "$Api/api$path"
        Method      = $method
        Headers     = @{ Authorization = "Bearer $token" }
        ContentType = 'application/json'
    }
    if ($method -eq 'Post') { $request.Body = if ($body) { $body | ConvertTo-Json -Depth 5 } else { '{}' } }
    Invoke-RestMethod @request
}

# Runs one workflow step and prints it, so a failure shows exactly which step broke
function Step([string]$token, $ticket, [string]$step, $body = $null) {
    Write-Host "   $step"
    [void](Invoke-Api $token 'Post' "/tickets/$($ticket.id)/$step" $body)
}

function New-Ticket([string]$title, [string]$description, [string]$repo, [string]$sha, [string]$priority) {
    Write-Host "- $title"
    Invoke-Api $client 'Post' '/tickets' @{
        title = $title; description = $description; repositoryUrl = $repo
        branch = 'main'; commitSha = $sha; priority = $priority
    }
}

function Fix([string]$branch, [string]$sha, [string]$summary) {
    @{ branch = $branch; commitSha = $sha; summary = $summary }
}

# --- 3. Log in as every role ---
$client  = Get-Token 'client@mizan.local'
$manager = Get-Token 'manager@mizan.local'
$leader  = Get-Token 'leader@mizan.local'
$member1 = Get-Token 'member1@mizan.local'
$member2 = Get-Token 'member2@mizan.local'

$team = (Invoke-Api $manager 'Get' '/teams')[0]
$m1 = ($team.members | Where-Object name -eq 'Member One').id
$m2 = ($team.members | Where-Object name -eq 'Member Two').id
$route = @{ teamId = $team.id }

$shop    = 'https://github.com/demo-client/shop-app'
$billing = 'https://github.com/demo-client/billing-service'

# --- 4. Tickets, oldest first (the list shows the newest on top) ---
Write-Host 'Creating tickets...'

# Closed: the happy path, start to finish
$t = New-Ticket 'Checkout total ignores discount codes' `
    'Applying a valid code shows the discount in the cart, but the checkout total is still the full price.' `
    $shop 'a41c9e2' 'Medium'
Step $manager $t 'route' $route
Step $leader  $t 'assign' @{ memberIds = @($m1) }
Step $member1 $t 'submit' (Fix 'fix/discount-total' 'b7d203f' 'The checkout total now subtracts the discount before tax. Added a test with a 10% code.')
Step $leader  $t 'leader-approve'
Step $manager $t 'manager-approve'
Step $client  $t 'close'

# Rejected: not a code correction, so the manager turns it down
$t = New-Ticket 'Add a cryptocurrency payment option' `
    'We would like customers to be able to pay with Bitcoin at checkout.' `
    $shop 'c03e7a1' 'Low'
Step $manager $t 'reject'

# Reopened: the client tried the fix and it was not enough
$t = New-Ticket 'Login page crashes on empty email' `
    'Pressing "Sign in" with the email box empty shows a white screen instead of an error message.' `
    $shop '9f2b6d4' 'High'
Step $manager $t 'route' $route
Step $leader  $t 'assign' @{ memberIds = @($m2) }
Step $member2 $t 'submit' (Fix 'fix/empty-email' 'e18a5c0' 'The form now checks for an empty email before calling the server and shows "Email is required".')
Step $leader  $t 'leader-approve'
Step $manager $t 'manager-approve'
Step $client  $t 'reopen'

# Response Sent: waiting for the client to accept or reopen (good for the live demo)
$t = New-Ticket 'Search returns duplicate products' `
    'Searching "shoes" lists some products twice when they belong to more than one category.' `
    $shop '0a1b2c3' 'Medium'
Step $manager $t 'route' $route
Step $leader  $t 'assign' @{ memberIds = @($m1) }
Step $member1 $t 'submit' (Fix 'fix/dup-search' 'b2c3d4e' 'Added DISTINCT to the search query so each product appears once, plus a test with a product in two categories.')
Step $leader  $t 'leader-approve'
Step $manager $t 'manager-approve'

# Manager Review, after one round trip: the manager sent the first fix back
$t = New-Ticket 'Invoice PDF shows the wrong VAT total' `
    'On invoices with more than one item, the VAT line is calculated from the first item only.' `
    $billing '4d8e1f7' 'High'
Step $manager $t 'route' $route
Step $leader  $t 'assign' @{ memberIds = @($m1, $m2) }
Step $member1 $t 'submit' (Fix 'fix/vat-total' '6a0c3b9' 'VAT is now summed over all invoice lines.')
Step $leader  $t 'leader-approve'
Step $manager $t 'manager-send-back'
Step $member2 $t 'submit' (Fix 'fix/vat-total' '7f5e2d1' 'VAT is summed per line and rounded once at the end, so the total matches the accounting system. Added tests for 1, 2 and 10 lines.')
Step $leader  $t 'leader-approve'

# Leader Review: a member has submitted, the leader hasn't looked yet
$t = New-Ticket 'Dark mode setting resets after refresh' `
    'Turning on dark mode works, but after a page refresh the site is back to light mode.' `
    $shop '2e9d4a6' 'Low'
Step $manager $t 'route' $route
Step $leader  $t 'assign' @{ memberIds = @($m2) }
Step $member2 $t 'submit' (Fix 'fix/dark-mode-persist' '8c1f0b3' 'The chosen theme is now saved in localStorage and read when the app starts.')

# In Progress: assigned, being worked on
$t = New-Ticket 'Profile picture upload ignores the size limit' `
    'The page says 2 MB maximum, but a 15 MB photo uploads without any error and slows the profile page down.' `
    $shop '5b7a9c8' 'Medium'
Step $manager $t 'route' $route
Step $leader  $t 'assign' @{ memberIds = @($m1) }

# Routed: on the team's desk, nobody assigned yet (the leader can assign live)
$t = New-Ticket 'Orders API returns 500 when the cart is empty' `
    'Calling POST /orders with an empty cart crashes with a 500 instead of returning a clear 400 error.' `
    $billing '3c6f8e0' 'Critical'
Step $manager $t 'route' $route

# Submitted: new, waiting for the manager (the manager can route live)
$t = New-Ticket 'Password reset email never arrives' `
    'Clicking "Forgot password" shows a success message, but no email is ever sent.' `
    $shop 'd9a4e27' 'High'

Write-Host ''
Write-Host 'Done: 9 tickets, one in every status.'
