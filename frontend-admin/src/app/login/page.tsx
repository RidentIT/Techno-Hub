import { Suspense } from "react";

import { LoginForm } from "@/components/login-form";

export const metadata = { title: "Sign in — Techno Hub" };

export default function LoginPage() {
  return (
    <main className="flex min-h-screen items-center justify-center bg-muted/40 px-4 py-12">
      <div className="w-full max-w-md">
        <div className="mb-8 text-center">
          <h1 className="text-2xl font-bold tracking-tight">Techno Hub</h1>
          <p className="mt-1 text-sm text-muted-foreground">Staff console</p>
        </div>

        {/* LoginForm reads the ?next= search param, so it needs a Suspense boundary. */}
        <Suspense fallback={null}>
          <LoginForm />
        </Suspense>

        <p className="mt-6 text-center text-xs text-muted-foreground">
          Staff accounts are created by an administrator. There is no self-registration.
        </p>
      </div>
    </main>
  );
}
