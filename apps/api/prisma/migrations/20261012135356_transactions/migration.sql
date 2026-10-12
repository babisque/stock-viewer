-- CreateTable
CREATE TABLE "Transaction" (
    "id" TEXT NOT NULL PRIMARY KEY,
    "positionId" TEXT NOT NULL,
    "type" TEXT NOT NULL,
    "date" DATETIME NOT NULL,
    "quantity" DECIMAL NOT NULL,
    "unitPrice" DECIMAL NOT NULL,
    "fees" DECIMAL NOT NULL DEFAULT 0,
    "createdAt" DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "Transaction_positionId_fkey" FOREIGN KEY ("positionId") REFERENCES "Position" ("id") ON DELETE CASCADE ON UPDATE CASCADE
);

-- CreateIndex
CREATE INDEX "Transaction_positionId_date_idx" ON "Transaction"("positionId", "date");

-- Backfill: every existing position becomes a single initial BUY at its
-- average price, dated on the (local) day it was created. Position rows are
-- kept unchanged. The transaction reuses the position's createdAt so it
-- sorts before anything recorded later on the same day.
INSERT INTO "Transaction" ("id", "positionId", "type", "date", "quantity", "unitPrice", "fees", "createdAt")
SELECT
    'init_' || "id",
    "id",
    'BUY',
    strftime('%Y-%m-%dT00:00:00.000+00:00', "createdAt", 'localtime'),
    "quantity",
    "averagePrice",
    0,
    "createdAt"
FROM "Position";
