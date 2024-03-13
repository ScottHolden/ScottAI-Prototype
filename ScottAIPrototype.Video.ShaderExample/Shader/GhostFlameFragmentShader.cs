using Silk.NET.OpenGL;

namespace ScottAIPrototype;

internal class GhostFlameFragmentShader : IShader
{
    public ShaderType ShaderType => ShaderType.FragmentShader;

    public string Source => """
        #version 330 core
        uniform vec2 iResolution;
        uniform vec3 iActivity;
        uniform float iTime;
        out vec4 fragColor;
        vec2 fragCoord = gl_FragCoord.xy;
        
        float noise(vec3 p)
        {
        	vec3 i = floor(p);
        	vec4 a = dot(i, vec3(1., 57., 21.)) + vec4(0., 57., 21., 78.);
        	vec3 f = cos((p-i)*acos(-1.))*(-.5)+.5;
        	a = mix(sin(cos(a)*a),sin(cos(1.+a)*(1.+a)), f.x);
        	a.xy = mix(a.xz, a.yw, f.y);
        	return mix(a.x, a.y, f.z);
        }
        
        float sphere(vec3 p, vec4 spr)
        {
        	return length(spr.xyz-p) - spr.w;
        }
        
        float flame(vec3 p)
        {
        	float d = sphere(p*vec3(iActivity.y,.5,1.), vec4(.0,-1.,.0,1.));
        	return d + (noise(p+vec3(.0,iTime*2.,.0)) + noise(p*3.)*.5)*.25*(p.y);
        }
        
        vec4 raymarch(vec3 org, vec3 dir)
        {
        	float d = 0.0, glow = 0.0, eps = 0.02;
        	vec3 p = org;
        	bool glowed = false;
        	for(int i=0; i<64; i++)
        	{
        		d = min(100.-length(p) , abs(flame(p))) + eps;
        		p += d * dir;
        		if( d>eps )
        		{
        			if(flame(p) < .0)
        				glowed=true;
        			if(glowed)
        				glow = float(i)/64.;
        		}
        	}
        	return vec4(p,glow);
        }
        
        void main()
        {
        	vec2 v = -1.0 + 2.0 * fragCoord.xy / iResolution.xy;
        	v.x *= iResolution.x/iResolution.y;
        	vec3 org = vec3(0., -2., 4.);
        	vec3 dir = normalize(vec3(v.x*1.6, -v.y, -1.5));
        	vec4 p = raymarch(org, dir);
        	vec4 col = mix(vec4(0.1,.5,.1,1.), vec4(0.1,.5,iActivity.x,1.), p.y*.02+.4);
        	fragColor = mix(vec4(0.), col, pow(p.w*2.,4.)) * iActivity.z;
        }
        """;
}