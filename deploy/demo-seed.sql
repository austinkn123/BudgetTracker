-- Demo data: a fictional person with a year of history, so the app can be shown without real finances.
-- Run it through deploy/demo-account.ps1, which creates the Cognito login and passes its sub as :sub.
-- Re-runnable: it wipes the demo user's data and rebuilds it relative to today, so run it again before a demo.
--
-- The "linked banks" are rows only. Their access token is a placeholder that fails decryption, which the app
-- treats as "skip this item": sync-on-open fails silently, and the manual Refresh in Settings reports an error.

SELECT set_config('demo.sub', :'sub', false), set_config('demo.today', :'today', false);

DO $$
DECLARE
    uid int;
    -- The caller's local day, not the server's: Neon runs on UTC, so evening swipes would land on "tomorrow".
    today date := current_setting('demo.today')::date;
    this_month date := date_trunc('month', today)::date;
    first_month date := (this_month - interval '11 months')::date; -- a full year, Money Map's default window
    plan_id int;
BEGIN
    PERFORM setseed(0.42); -- same "random" spend on every run

    SELECT "Id" INTO uid FROM "Users" WHERE "CognitoSub" = current_setting('demo.sub');
    IF uid IS NULL THEN
        INSERT INTO "Users" ("CognitoSub") VALUES (current_setting('demo.sub')) RETURNING "Id" INTO uid;
    END IF;

    -- The wipe below would destroy real data, so refuse any user with a real bank link (i.e. not the demo user).
    IF EXISTS (SELECT 1 FROM "PlaidItems" WHERE "UserId" = uid AND "PlaidItemId" NOT LIKE 'demo-%') THEN
        RAISE EXCEPTION 'User % has a real Plaid link; refusing to wipe it. Is :sub the demo login?', uid;
    END IF;

    DELETE FROM "Transactions" WHERE "AccountId" IN (SELECT "Id" FROM "Accounts" WHERE "UserId" = uid);
    DELETE FROM "BudgetPlanEntries" WHERE "BudgetPlanId" IN (SELECT "Id" FROM "BudgetPlans" WHERE "UserId" = uid);
    DELETE FROM "BudgetPlans" WHERE "UserId" = uid;
    DELETE FROM "Categories" WHERE "UserId" = uid;
    DELETE FROM "Accounts" WHERE "UserId" = uid;
    DELETE FROM "PlaidAccounts" WHERE "PlaidItemId" IN (SELECT "Id" FROM "PlaidItems" WHERE "UserId" = uid);
    DELETE FROM "PlaidItems" WHERE "UserId" = uid;

    -- Categories
    CREATE TEMP TABLE demo_cat (name text, id int) ON COMMIT DROP;
    WITH ins AS (
        INSERT INTO "Categories" ("UserId", "Name", "CategoryType")
        SELECT uid, name, kind FROM (VALUES
            ('Paycheck', 'Income'), ('Interest', 'Income'),
            ('Rent', 'Expense'), ('Groceries', 'Expense'), ('Power, Internet & Phone', 'Expense'), ('Transit', 'Expense'),
            ('Student Loan', 'Expense'), ('Renters Insurance', 'Expense'), ('Dog', 'Expense'), ('Health', 'Expense'),
            ('Coffee & Food', 'Expense'), ('Climbing & Ski Gear', 'Expense'), ('Clothes', 'Expense'),
            ('Streaming', 'Expense'), ('Gifts', 'Expense'), ('Trips', 'Expense'),
            ('Card Payment', 'Transfer'), ('Savings', 'Transfer')
        ) v(name, kind)
        RETURNING "Id", "Name")
    INSERT INTO demo_cat SELECT "Name", "Id" FROM ins;

    -- Budget plan, in effect since the first seeded month
    INSERT INTO "BudgetPlans" ("UserId", "Name", "PlanMonth", "NetIncomeMonthly", "IsActive")
    VALUES (uid, 'Denver Budget', first_month, 4750, true)
    RETURNING "Id" INTO plan_id;

    INSERT INTO "BudgetPlanEntries"
        ("BudgetPlanId", "CategoryId", "LineType", "Bucket", "Cadence", "Amount", "MonthlyEquivalent", "IsStressFactor", "SortOrder")
    SELECT plan_id, c.id, e.line, e.bucket, e.cadence, e.amount,
           CASE e.cadence WHEN 'Annual' THEN round(e.amount / 12, 2) ELSE e.amount END, e.stress, e.sort
    FROM (VALUES
        ('Paycheck',                'Income',  'Core',   'Monthly', 4750, false, 0),
        ('Rent',                    'Expense', 'Core',   'Monthly', 1650, false, 1),
        ('Groceries',               'Expense', 'Core',   'Monthly',  450, false, 2),
        ('Power, Internet & Phone', 'Expense', 'Core',   'Monthly',  180, false, 3),
        ('Transit',                 'Expense', 'Core',   'Monthly',  130, false, 4),
        ('Student Loan',            'Expense', 'Core',   'Monthly',  310, false, 5),
        ('Renters Insurance',       'Expense', 'Core',   'Monthly',   14, false, 6),
        ('Dog',                     'Expense', 'Core',   'Monthly',  140, false, 7),
        ('Health',                  'Expense', 'Core',   'Monthly',   40, false, 8),
        ('Coffee & Food',           'Expense', 'Buffer', 'Monthly',  260, false, 9),
        ('Climbing & Ski Gear',     'Expense', 'Buffer', 'Monthly',  150, true,  10),
        ('Clothes',                 'Expense', 'Buffer', 'Monthly',   80, false, 11),
        ('Streaming',               'Expense', 'Buffer', 'Monthly',   35, false, 12),
        ('Gifts',                   'Expense', 'Buffer', 'Monthly',   50, false, 13),
        ('Trips',                   'Expense', 'Buffer', 'Annual',  1800, false, 14)
    ) e(category, line, bucket, cadence, amount, stress, sort)
    JOIN demo_cat c ON c.name = e.category;

    -- Transactions, signed the app's way: negative = money out of that account
    CREATE TEMP TABLE demo_tx (acct text, category text, type text, amount numeric(18,2), at date, payee text, plaid text, transfer_to text) ON COMMIT DROP;

    -- Monthly recurring. jitter adds up to that much on top, for bills that vary.
    INSERT INTO demo_tx
    SELECT r.acct, r.category, r.type, r.amount + sign(r.amount) * round((random() * r.jitter)::numeric, 2),
           m + (r.day - 1), r.payee, r.plaid, r.transfer_to
    FROM (SELECT generate_series(first_month, today, interval '1 month')::date AS m) months
    CROSS JOIN (VALUES
        ('chk',  'Paycheck',                'Income',    2375.00,  1, 0,  'Bluebird Health Payroll', 'INCOME',              NULL),
        ('chk',  'Paycheck',                'Income',    2375.00, 15, 0,  'Bluebird Health Payroll', 'INCOME',              NULL),
        ('chk',  'Rent',                    'Expense',  -1650.00,  1, 0,  'Cherry Creek Lofts',      'RENT_AND_UTILITIES',  NULL),
        ('chk',  'Power, Internet & Phone', 'Expense',    -48.00, 10, 45, 'Xcel Energy',             'RENT_AND_UTILITIES',  NULL),
        ('chk',  'Student Loan',            'Expense',   -310.00, 20, 0,  'Nelnet',                  'LOAN_PAYMENTS',       NULL),
        ('chk',  'Savings',                 'Transfer',  -400.00, 16, 0,  'Transfer to Savings',     'TRANSFER_OUT',        'sav'),
        ('sav',  'Savings',                 'Transfer',   400.00, 16, 0,  'Transfer from Checking',  'TRANSFER_IN',         'chk'),
        ('sav',  'Interest',                'Income',      21.00, 28, 9,  'Dividend Earned',         'INCOME',              NULL),
        ('card', 'Power, Internet & Phone', 'Expense',    -65.00, 12, 0,  'Xfinity',                 'RENT_AND_UTILITIES',  NULL),
        ('card', 'Power, Internet & Phone', 'Expense',    -25.00, 19, 0,  'Visible Wireless',        'RENT_AND_UTILITIES',  NULL),
        ('card', 'Transit',                 'Expense',    -88.00,  1, 0,  'RTD Mobile Tickets',      'TRANSPORTATION',      NULL),
        ('card', 'Renters Insurance',       'Expense',    -13.75,  5, 0,  'Lemonade Insurance',      'GENERAL_SERVICES',    NULL),
        ('card', 'Dog',                     'Expense',    -64.99,  7, 0,  'Chewy',                   'GENERAL_MERCHANDISE', NULL),
        ('card', 'Climbing & Ski Gear',     'Expense',    -79.00,  3, 0,  'Movement Climbing Gym',   'PERSONAL_CARE',       NULL),
        ('card', 'Streaming',               'Expense',    -18.99,  9, 0,  'Hulu',                    'ENTERTAINMENT',       NULL),
        ('card', 'Streaming',               'Expense',    -13.99, 14, 0,  'YouTube Premium',         'ENTERTAINMENT',       NULL)
    ) r(acct, category, type, amount, day, jitter, payee, plaid, transfer_to);

    -- Everyday card spend: n purchases a month between lo and hi, on random days
    INSERT INTO demo_tx
    SELECT 'card', v.category, 'Expense', -round((v.lo + random() * (v.hi - v.lo))::numeric, 2),
           m + floor(random() * extract(day from m + interval '1 month - 1 day'))::int, v.payee, v.plaid, NULL
    FROM (SELECT generate_series(first_month, today, interval '1 month')::date AS m) months
    CROSS JOIN (VALUES
        ('Groceries',           'King Soopers',          'FOOD_AND_DRINK',      5, 40, 110),
        ('Groceries',           'Natural Grocers',       'FOOD_AND_DRINK',      2, 20, 55),
        ('Coffee & Food',       'Huckleberry Roasters',  'FOOD_AND_DRINK',      6, 4.50, 8),
        ('Coffee & Food',       'Illegal Pete''s',       'FOOD_AND_DRINK',      3, 11, 16),
        ('Coffee & Food',       'Sweetgreen',            'FOOD_AND_DRINK',      3, 13, 18),
        ('Coffee & Food',       'Snooze an A.M. Eatery', 'FOOD_AND_DRINK',      1, 28, 45),
        ('Coffee & Food',       'Denver Beer Co',        'FOOD_AND_DRINK',      2, 18, 40),
        ('Transit',             'Lime',                  'TRANSPORTATION',      3, 3, 9),
        ('Transit',             'Lyft',                  'TRANSPORTATION',      1, 14, 32),
        ('Climbing & Ski Gear', 'REI Co-op',             'GENERAL_MERCHANDISE', 1, 20, 180),
        ('Clothes',             'Buffalo Exchange',      'GENERAL_MERCHANDISE', 1, 25, 90),
        ('Dog',                 'Petco',                 'GENERAL_MERCHANDISE', 1, 10, 40),
        ('Health',              'Walgreens',             'MEDICAL',             1, 8, 35),
        ('Gifts',               'Etsy',                  'GENERAL_MERCHANDISE', 1, 20, 60)
    ) v(category, payee, plaid, n, lo, hi)
    CROSS JOIN LATERAL generate_series(1, v.n);

    -- One-offs: a ski pass, a trip, a vet bill, a gear return (a positive Expense nets against spending),
    -- and a few left to categorize
    INSERT INTO demo_tx VALUES
        ('card', 'Trips',               'Expense', -1079.00, (this_month - interval '2 months')::date + 4,  'Ikon Pass',             'TRAVEL',              NULL),
        ('card', 'Trips',               'Expense',  -212.40, (this_month - interval '5 months')::date + 11, 'Frontier Airlines',     'TRAVEL',              NULL),
        ('card', 'Trips',               'Expense',  -436.18, (this_month - interval '5 months')::date + 24, 'Airbnb',                'TRAVEL',              NULL),
        ('card', 'Climbing & Ski Gear', 'Expense',  -389.95, (this_month - interval '4 months')::date + 13, 'REI Co-op',             'GENERAL_MERCHANDISE', NULL),
        ('card', 'Climbing & Ski Gear', 'Expense',  -249.00, (this_month - interval '1 month')::date + 6,   'Evo Denver',            'GENERAL_MERCHANDISE', NULL),
        ('card', 'Climbing & Ski Gear', 'Expense',    64.95, (this_month - interval '1 month')::date + 9,   'REI Co-op',             'GENERAL_MERCHANDISE', NULL),
        ('card', 'Dog',                 'Expense',  -285.40, (this_month - interval '6 months')::date + 9,  'VCA Animal Hospital',   'GENERAL_SERVICES',    NULL),
        ('card', 'Clothes',             'Expense',  -149.00, (this_month - interval '3 months')::date + 18, 'Patagonia',             'GENERAL_MERCHANDISE', NULL),
        ('chk',  NULL,                  'Expense',   -30.00, today - 4,                                    'Cash App',              'TRANSFER_OUT',        NULL),
        ('card', NULL,                  'Expense',   -23.40, today - 3,                                    'Ace Hardware',          'HOME_IMPROVEMENT',    NULL),
        ('card', NULL,                  'Expense',    -9.50, today - 1,                                    'Little Man Ice Cream',  'FOOD_AND_DRINK',      NULL);

    DELETE FROM demo_tx WHERE at > today;

    -- The card is paid in full from checking on the 3rd for the month before
    INSERT INTO demo_tx
    SELECT side.acct, 'Card Payment', 'Transfer', side.sign * p.total, p.month + interval '1 month 2 days',
           side.payee, side.plaid, side.transfer_to
    FROM (SELECT date_trunc('month', at)::date AS month, sum(amount) AS total
          FROM demo_tx WHERE acct = 'card' GROUP BY 1) p
    CROSS JOIN (VALUES ('chk', 1, 'Discover E-Payment', 'LOAN_PAYMENTS', 'card'),
                       ('card', -1, 'Discover Payment Received', 'TRANSFER_IN', 'chk')) side(acct, sign, payee, plaid, transfer_to)
    WHERE p.month + interval '1 month 2 days' <= today;

    -- Accounts, and the Plaid rows that put them on Linked Accounts and Cash Flow.
    -- Balances are an opening amount plus the seeded history, so Cash Flow's walk back from today stays consistent.
    CREATE TEMP TABLE demo_acct (key text, id int, plaid_account_id text) ON COMMIT DROP;
    WITH v(key, name, type) AS (VALUES
            ('chk',  'Ent Credit Union - Free Checking (••3307)', 'depository'),
            ('sav',  'Ent Credit Union - Savings (••5512)',       'depository'),
            ('card', 'Discover - Discover it Card (••6620)',      'credit')),
        ins AS (
            INSERT INTO "Accounts" ("UserId", "Name", "AccountType")
            SELECT uid, name, type FROM v
            RETURNING "Id", "Name")
    INSERT INTO demo_acct
    SELECT v.key, ins."Id", 'demo-' || uid || '-' || v.key FROM ins JOIN v ON v.name = ins."Name";

    WITH items AS (
        INSERT INTO "PlaidItems" ("UserId", "PlaidItemId", "InstitutionId", "InstitutionName", "AccessTokenEncrypted", "LastSyncedAt")
        VALUES (uid, 'demo-' || uid || '-ent',      'ins_demo_ent',      'Ent Credit Union', 'ZGVtby1ub3QtYS1yZWFsLXRva2Vu', now() AT TIME ZONE 'utc' - interval '1 hour'),
               (uid, 'demo-' || uid || '-discover', 'ins_demo_discover', 'Discover',         'ZGVtby1ub3QtYS1yZWFsLXRva2Vu', now() AT TIME ZONE 'utc' - interval '1 hour')
        RETURNING "Id", "InstitutionName")
    INSERT INTO "PlaidAccounts" ("PlaidItemId", "PlaidAccountId", "Mask", "Name", "AccountType", "AccountSubtype", "CurrentBalance")
    SELECT i."Id", a.plaid_account_id, v.mask, v.name, v.type, v.subtype,
           v.opening + v.sign * coalesce((SELECT sum(amount) FROM demo_tx t WHERE t.acct = v.key), 0)
    FROM (VALUES
        ('Ent Credit Union', 'chk',  '3307', 'Free Checking',    'depository', 'checking',    1800.00,  1),
        ('Ent Credit Union', 'sav',  '5512', 'Savings',          'depository', 'savings',     6500.00,  1),
        ('Discover',         'card', '6620', 'Discover it Card', 'credit',     'credit card',    0.00, -1) -- owed, so positive
    ) v(institution, key, mask, name, type, subtype, opening, sign)
    JOIN items i ON i."InstitutionName" = v.institution
    JOIN demo_acct a ON a.key = v.key;

    INSERT INTO "Transactions"
        ("AccountId", "CategoryId", "TransactionType", "Amount", "OccurredAt", "Payee", "TransferAccountId",
         "PlaidTransactionId", "PlaidAccountId", "PlaidCategoryPrimary", "IsImported", "IsPending")
    SELECT a.id, c.id, t.type, t.amount, t.at, t.payee, x.id,
           'demo-' || gen_random_uuid(), a.plaid_account_id, t.plaid, true,
           t.acct = 'card' AND t.at >= today - 1 -- the newest card swipes haven't posted yet
    FROM demo_tx t
    JOIN demo_acct a ON a.key = t.acct
    LEFT JOIN demo_acct x ON x.key = t.transfer_to
    LEFT JOIN demo_cat c ON c.name = t.category;

    RAISE NOTICE 'Demo user % seeded with % transactions.', uid, (SELECT count(*) FROM demo_tx);
END $$;
