import { redirect } from "next/navigation";

/** The console has no marketing homepage; send everyone to the dashboard, which guards itself. */
export default function HomePage() {
  redirect("/dashboard");
}
