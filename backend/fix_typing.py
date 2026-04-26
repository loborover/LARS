import os
import re

for root, dirs, files in os.walk('.'):
    if 'venv' in root or '.pyc' in root:
        continue
    for file in files:
        if file.endswith('.py'):
            path = os.path.join(root, file)
            with open(path, 'r') as f:
                content = f.read()
            
            # Simple replace is mostly safe for these if used in type hints
            # But let's be careful with string literals containing "List["
            # We'll just regex replace boundaries
            new_content = re.sub(r'\blist\[', 'List[', content)
            new_content = re.sub(r'\btuple\[', 'Tuple[', new_content)
            new_content = re.sub(r'\bdict\[', 'Dict[', new_content)
            
            if new_content != content:
                # Make sure Tuple, List, Dict are imported
                imports_to_add = []
                if 'Tuple[' in new_content and 'Tuple' not in new_content.split('\n')[0:20]:
                    imports_to_add.append('Tuple')
                
                # A brute force way: just add "from typing import List, Tuple, Dict" if missing
                if 'from typing import' in new_content:
                    lines = new_content.split('\n')
                    for i, line in enumerate(lines):
                        if line.startswith('from typing import'):
                            missing = [t for t in ['List', 'Tuple', 'Dict', 'Any', 'Optional'] if t not in line and t in new_content]
                            if missing:
                                lines[i] = line + ', ' + ', '.join(missing)
                            break
                    new_content = '\n'.join(lines)
                else:
                    new_content = 'from typing import List, Tuple, Dict, Any, Optional\n' + new_content

                with open(path, 'w') as f:
                    f.write(new_content)
                print(f"Fixed {path}")
