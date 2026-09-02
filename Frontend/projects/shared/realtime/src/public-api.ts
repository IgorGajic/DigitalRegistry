/*
 * The SignalR hub connection.
 *
 * Its own entry point for the same reason as `shared/ui`, and more sharply: `@microsoft/signalr` is
 * the single largest dependency either application has, and only three lazy screens open a hub.
 */

export * from './lib/realtime.service';
