export function LoadingBlock({ message = 'Loading...' }: { message?: string }) {
  return <div className="loading-block">{message}</div>;
}
