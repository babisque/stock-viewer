import { setupServer } from "msw/node";

// Shared msw server. Handlers are added per test with server.use(); any
// request without a handler fails the test, so real providers are never hit.
export const server = setupServer();
