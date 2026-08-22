"use client";

import { useMemo } from "react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import type { ScopeGroup } from "@/lib/types";

interface ScopePickerProps {
  /** Catalogue from GET /api/staff/scopes, already grouped by module. */
  groups: ScopeGroup[];
  selected: string[];
  onChange: (scopes: string[]) => void;
  disabled?: boolean;
  /** Rendered instead of the checkboxes, e.g. to explain why an Admin has no scopes. */
  notice?: string;
  idPrefix?: string;
}

/**
 * Grouped scope checkboxes.
 *
 * The list comes from the API rather than a hardcoded array, so adding a scope to
 * ScopeNames.All on the backend makes it appear here with no frontend change.
 */
export function ScopePicker({
  groups,
  selected,
  onChange,
  disabled = false,
  notice,
  idPrefix = "scope",
}: ScopePickerProps) {
  const selectedSet = useMemo(() => new Set(selected), [selected]);

  if (notice) {
    return <p className="text-sm text-muted-foreground">{notice}</p>;
  }

  function toggle(key: string, checked: boolean) {
    if (checked) {
      if (selectedSet.has(key)) return;
      onChange([...selected, key]);
      return;
    }

    onChange(selected.filter((scope) => scope !== key));
  }

  function toggleModule(group: ScopeGroup, checked: boolean) {
    const keys = group.scopes.map((scope) => scope.key);

    if (checked) {
      const merged = new Set(selected);
      keys.forEach((key) => merged.add(key));
      onChange([...merged]);
      return;
    }

    onChange(selected.filter((scope) => !keys.includes(scope)));
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm text-muted-foreground">
          {selected.length === 0
            ? "No permissions selected — the account will have no access."
            : `${selected.length} permission${selected.length === 1 ? "" : "s"} selected.`}
        </p>

        {selected.length > 0 && !disabled ? (
          <Button type="button" variant="ghost" size="sm" onClick={() => onChange([])}>
            Clear all
          </Button>
        ) : null}
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        {groups.map((group) => {
          const keys = group.scopes.map((scope) => scope.key);
          const selectedCount = keys.filter((key) => selectedSet.has(key)).length;
          const allSelected = selectedCount === keys.length;

          return (
            <fieldset
              key={group.module}
              className="rounded-md border border-border bg-card p-3"
              disabled={disabled}
            >
              <legend className="sr-only">{group.module}</legend>

              <div className="mb-2 flex items-center justify-between gap-2">
                <div className="flex items-center gap-2">
                  <span className="text-sm font-semibold">{group.module}</span>
                  {selectedCount > 0 ? (
                    <Badge variant="secondary" className="text-[10px]">
                      {selectedCount}/{keys.length}
                    </Badge>
                  ) : null}
                </div>

                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  className="h-6 px-2 text-xs"
                  disabled={disabled}
                  onClick={() => toggleModule(group, !allSelected)}
                >
                  {allSelected ? "None" : "All"}
                </Button>
              </div>

              <div className="space-y-2">
                {group.scopes.map((scope) => {
                  const inputId = `${idPrefix}-${scope.key}`;

                  return (
                    <div key={scope.key} className="flex items-start gap-2">
                      <Checkbox
                        id={inputId}
                        checked={selectedSet.has(scope.key)}
                        disabled={disabled}
                        onCheckedChange={(checked) => toggle(scope.key, checked === true)}
                        className="mt-0.5"
                      />
                      <div className="grid gap-0.5 leading-none">
                        <Label htmlFor={inputId} className="cursor-pointer font-mono text-xs">
                          {scope.key}
                        </Label>
                        <p className="text-xs text-muted-foreground">{scope.description}</p>
                      </div>
                    </div>
                  );
                })}
              </div>
            </fieldset>
          );
        })}
      </div>
    </div>
  );
}
