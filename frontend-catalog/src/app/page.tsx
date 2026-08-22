const PLANNED = [
  {
    title: "Product catalogue",
    detail: "Browse CPUs, GPUs, motherboards, storage, peripherals and prebuilt systems by category.",
  },
  {
    title: "Custom PC builder",
    detail: "Pick parts with compatibility checks and see a running total as the build comes together.",
  },
  {
    title: "Quotation requests",
    detail:
      "Send a build or parts list to the shop for a formal quote — name and contact details only, no account.",
  },
];

export default function HomePage() {
  return (
    <main className="mx-auto flex min-h-screen max-w-3xl flex-col justify-center gap-10 px-6 py-16">
      <header className="space-y-3">
        <p className="text-sm font-medium uppercase tracking-widest opacity-60">Techno Hub</p>
        <h1 className="text-4xl font-bold tracking-tight sm:text-5xl">
          Computer &amp; tech hardware
        </h1>
        <p className="max-w-prose text-lg opacity-80">
          The public catalogue and quotation builder are being built. No account will ever be needed —
          browse the catalogue and request a quote as a guest.
        </p>
      </header>

      <section className="space-y-4">
        <h2 className="text-sm font-semibold uppercase tracking-wider opacity-60">Coming next</h2>

        <ul className="space-y-3">
          {PLANNED.map((item) => (
            <li key={item.title} className="rounded-lg border border-current/15 p-4">
              <h3 className="font-semibold">{item.title}</h3>
              <p className="mt-1 text-sm opacity-75">{item.detail}</p>
            </li>
          ))}
        </ul>
      </section>

      <footer className="text-xs opacity-60">
        Staff sign-in lives in a separate application and is not reachable from here.
      </footer>
    </main>
  );
}
